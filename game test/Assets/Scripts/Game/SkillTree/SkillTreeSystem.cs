using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;

namespace IndieGame.Gameplay.SkillTree
{
    /// <summary>
    /// 技能解锁状态枚举。
    /// </summary>
    public enum SkillLearnState
    {
        // 前置未满足，无法学习
        Locked    = 0,
        // 前置已满足、SP 足够，可以学习
        Available = 1,
        // 已学习
        Learned   = 2,
    }

    /// <summary>
    /// 技能树系统（SkillTreeSystem）：
    /// 维护玩家的技能点（SP）与技能解锁状态，是技能树的纯数据 + 规则层。
    ///
    /// 设计说明：
    /// 1) 技能数据库通过 Inspector 注入（SkillDataSO 数组）；
    /// 2) 升级时自动监听 LevelChangedEvent 获得 SP（每级 spPerLevel 点）；
    /// 3) 学习技能走 TryLearnSkill，检查前置 + SP → 扣减 → 应用效果 → 广播；
    /// 4) 接入 ISaveable：存档保存 currentSP + learnedSkillIds 集合。
    /// </summary>
    public class SkillTreeSystem : SaveableMonoSingleton<SkillTreeSystem>
    {
        [Header("Config")]
        [Tooltip("所有技能的 SO 数据，在此注入整个技能数据库。")]
        [SerializeField] private SkillDataSO[] allSkills;

        [Tooltip("每升一级获得的技能点数量。")]
        [SerializeField] private int spPerLevel = 2;

        [Tooltip("初始技能点（未读档时生效）。")]
        [SerializeField] private int initialSP = 0;

        [Header("Runtime（只读调试）")]
        [SerializeField] private int currentSP;

        // 已学技能集合（HashSet 保证 O(1) 查询）
        private readonly HashSet<string> _learnedSkills = new HashSet<string>();
        // ID → SO 快速索引
        private Dictionary<string, SkillDataSO> _skillDatabase;
        private bool _isInitialized;

        public override string SaveID => "SkillTreeSystem";

        // --- 只读属性 ---
        public int CurrentSP
        {
            get { EnsureInitialized(); return currentSP; }
        }

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            EnsureInitialized();
            EnsureSaveRegistration(forceSearch: true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<LevelChangedEvent>(HandleLevelChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<LevelChangedEvent>(HandleLevelChanged);
        }

        // ─── 公开接口 ─────────────────────────────────────────

        /// <summary>
        /// 查询指定 ID 技能的解锁状态。
        /// </summary>
        public SkillLearnState GetLearnState(string skillId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(skillId)) return SkillLearnState.Locked;
            if (_learnedSkills.Contains(skillId)) return SkillLearnState.Learned;
            if (!_skillDatabase.TryGetValue(skillId, out SkillDataSO data)) return SkillLearnState.Locked;
            return ArePrerequisitesMet(data) ? SkillLearnState.Available : SkillLearnState.Locked;
        }

        /// <summary>
        /// 尝试学习指定技能（检查前置 + SP → 扣减 → 应用效果 → 广播）。
        /// 返回 true = 学习成功；false = 条件不足或已学。
        /// </summary>
        public bool TryLearnSkill(string skillId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(skillId)) return false;
            if (_learnedSkills.Contains(skillId)) return false;
            if (!_skillDatabase.TryGetValue(skillId, out SkillDataSO data)) return false;
            if (!ArePrerequisitesMet(data)) return false;
            if (currentSP < data.SpCost) return false;

            int cost = data.SpCost;
            currentSP -= cost;
            _learnedSkills.Add(skillId);

            RaiseSkillPointChanged(-cost);
            ApplySkillEffect(data);
            EventBus.Raise(new SkillLearnedEvent { SkillId = skillId });
            DebugTools.Log($"[SkillTreeSystem] 已学习技能：{data.SkillName}（剩余 SP：{currentSP}）");
            return true;
        }

        /// <summary>
        /// 判断某技能是否已学习。
        /// </summary>
        public bool IsLearned(string skillId)
        {
            EnsureInitialized();
            return _learnedSkills.Contains(skillId);
        }

        /// <summary>
        /// 获取指定分类下的所有技能 SO（用于 UI 重建技能网格）。
        /// </summary>
        public IReadOnlyList<SkillDataSO> GetSkillsByCategory(SkillTreeCategory category)
        {
            EnsureInitialized();
            var result = new List<SkillDataSO>();
            if (allSkills == null) return result;
            foreach (var skill in allSkills)
            {
                if (skill != null && skill.Category == category)
                    result.Add(skill);
            }
            return result;
        }

        // ─── ISaveable ────────────────────────────────────────

        public override object CaptureState()
        {
            EnsureInitialized();
            return new SkillTreeSaveState
            {
                CurrentSP       = currentSP,
                LearnedSkillIds = new List<string>(_learnedSkills)
            };
        }

        public override void RestoreState(object data)
        {
            EnsureInitialized();
            if (!(data is SkillTreeSaveState state)) return;

            _learnedSkills.Clear();
            currentSP = Mathf.Max(0, state.CurrentSP);

            if (state.LearnedSkillIds != null)
            {
                foreach (string id in state.LearnedSkillIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        _learnedSkills.Add(id);
                }
            }

            // ActionPointSystem 先于本系统注册并恢复（Bootstrap 顺序保证），
            // AP 上限已由其 RestoreState 正确还原，此处不需要重复调用效果。
            RaiseSkillPointChanged(0);
        }

        // ─── 内部逻辑 ─────────────────────────────────────────

        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            _skillDatabase = new Dictionary<string, SkillDataSO>();
            if (allSkills != null)
            {
                foreach (var s in allSkills)
                {
                    if (s != null && !string.IsNullOrWhiteSpace(s.SkillId))
                        _skillDatabase[s.SkillId] = s;
                }
            }

            currentSP = Mathf.Max(0, initialSP);
            _isInitialized = true;
            RaiseSkillPointChanged(0);
        }

        // 升级时自动获得 SP；Level <= 1 过滤初始广播，不给 SP。
        private void HandleLevelChanged(LevelChangedEvent evt)
        {
            EnsureInitialized();
            if (evt.Level <= 1) return;
            AddSP(spPerLevel, $"LevelUp:Lv{evt.Level}");
        }

        private void AddSP(int amount, string reason = null)
        {
            if (amount <= 0) return;
            currentSP += amount;
            DebugTools.Log($"[SkillTreeSystem] 获得 SP +{amount}（原因：{reason}），当前 SP：{currentSP}");
            RaiseSkillPointChanged(amount);
        }

        private bool ArePrerequisitesMet(SkillDataSO data)
        {
            if (data.PrerequisiteSkillIds == null || data.PrerequisiteSkillIds.Count == 0)
                return true;
            foreach (string prereqId in data.PrerequisiteSkillIds)
            {
                if (!_learnedSkills.Contains(prereqId))
                    return false;
            }
            return true;
        }

        private void ApplySkillEffect(SkillDataSO data)
        {
            switch (data.EffectType)
            {
                case SkillEffectType.IncreaseMaxActionPoint:
                    ActionPointSystem ap = ActionPointSystem.Instance;
                    if (ap != null)
                    {
                        int increase = Mathf.RoundToInt(data.EffectValue);
                        ap.SetMaxActionPoints(ap.MaxActionPoints + increase,
                                              $"SkillEffect:{data.SkillId}");
                    }
                    break;
                case SkillEffectType.None:
                default:
                    break;
            }
        }

        private void RaiseSkillPointChanged(int delta)
        {
            EventBus.Raise(new SkillPointChangedEvent
            {
                Current = currentSP,
                Delta   = delta
            });
        }

        // ─── 存档数据结构 ─────────────────────────────────────

        [Serializable]
        private class SkillTreeSaveState
        {
            // 当前可用技能点
            public int CurrentSP;
            // 已学技能 ID 列表
            public List<string> LearnedSkillIds;
        }
    }
}
