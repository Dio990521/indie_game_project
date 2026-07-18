using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗单位门面：
    /// 聚合一个战斗体上的全部战斗组件（属性/移动/索敌/普攻/充能/技能），
    /// 由 CombatManager 生成后显式调用 Initialize 完成受控初始化（不依赖 OnEnable 自注册，
    /// 保证对象池复用时初始化顺序可控）。
    /// 战斗体预制体组件清单：CombatUnit + CharacterStats + NavMeshAgent +
    /// CombatUnitMover + AutoTargeting + AutoAttackController + WeaponCharge + SkillCaster（敌人可省略后两个）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
    public class CombatUnit : MonoBehaviour
    {
        /// <summary> 所属阵营 </summary>
        public CombatTeam Team { get; private set; }

        /// <summary> 是否存活（MarkDead 后为 false，幂等保护的依据） </summary>
        public bool IsAlive { get; private set; }

        /// <summary> 是否主角战斗体（死亡即战败） </summary>
        public bool IsProtagonist { get; private set; }

        /// <summary> 角色属性（复用现有 CharacterStats：HP/伤害/死亡事件） </summary>
        public CharacterStats Stats { get; private set; }

        /// <summary> 武器战斗数据（普攻/充能/技能参数） </summary>
        public WeaponCombatDataSO WeaponData { get; private set; }

        // --- 战斗组件缓存（同物体上的兄弟组件） ---
        public CombatUnitMover Mover { get; private set; }
        public AutoTargeting Targeting { get; private set; }
        public AutoAttackController Attack { get; private set; }
        public WeaponCharge Charge { get; private set; }
        public SkillCaster Caster { get; private set; }

        /// <summary> 当前索敌目标（无索敌组件或无目标时为 null） </summary>
        public CombatUnit CurrentTarget => Targeting != null ? Targeting.CurrentTarget : null;

        // 已应用到 Stats 的武器加成快照（下场/死亡时精确撤销）
        private readonly List<StatModifierData> _appliedModifiers = new List<StatModifierData>();

        private void Awake()
        {
            // 缓存兄弟组件（一次性，避免运行中反复 GetComponent）
            Stats = GetComponent<CharacterStats>();
            Mover = GetComponent<CombatUnitMover>();
            Targeting = GetComponent<AutoTargeting>();
            Attack = GetComponent<AutoAttackController>();
            Charge = GetComponent<WeaponCharge>();
            Caster = GetComponent<SkillCaster>();
        }

        /// <summary>
        /// 受控初始化（由 CombatManager 在生成/上场后调用）：
        /// 注入阵营与武器数据、按等级重建属性（满血入场）、逐个初始化战斗组件。
        /// </summary>
        /// <param name="team">阵营</param>
        /// <param name="weapon">武器战斗数据（可空 = 无法普攻，仅站桩）</param>
        /// <param name="statConfig">数值配置</param>
        /// <param name="level">等级</param>
        /// <param name="isProtagonist">是否主角</param>
        /// <param name="config">战斗全局参数（节流间隔等）</param>
        public void Initialize(
            CombatTeam team,
            WeaponCombatDataSO weapon,
            CharacterStatConfigSO statConfig,
            int level,
            bool isProtagonist,
            CombatConfigSO config)
        {
            Team = team;
            WeaponData = weapon;
            IsProtagonist = isProtagonist;
            IsAlive = true;
            _appliedModifiers.Clear();

            // 注入数值配置并按等级重建（满血入场、经验清零）
            Stats.OverrideConfig(statConfig, level);

            float retargetInterval = config != null ? config.RetargetInterval : 0.25f;
            float repathInterval = config != null ? config.RepathInterval : 0.2f;

            if (Mover != null) Mover.Initialize(Stats.MoveSpeed.Value, repathInterval);
            if (Targeting != null) Targeting.Initialize(this, retargetInterval);
            if (Attack != null) Attack.Initialize(this);
            if (Charge != null) Charge.Initialize(this, weapon);
            if (Caster != null) Caster.Initialize(this);

            SetCombatComponentsEnabled(true);
        }

        /// <summary>
        /// 应用武器属性加成（主角战斗体同步已装备武器的 Modifiers 用）：
        /// 直接写入 Stats 的对应 Stat，不经过 WeaponEquipController（避免背包摘出联动）。
        /// </summary>
        public void ApplyStatModifiers(List<StatModifierData> modifiers)
        {
            if (modifiers == null || Stats == null) return;
            for (int i = 0; i < modifiers.Count; i++)
            {
                Stat stat = Stats.GetStat(modifiers[i].Type);
                if (stat == null) continue;
                stat.AddModifier(modifiers[i].Value);
                _appliedModifiers.Add(modifiers[i]);
            }
            // 加成可能影响移速，刷新 NavMeshAgent 速度
            if (Mover != null) Mover.SetMoveSpeed(Stats.MoveSpeed.Value);
        }

        /// <summary>
        /// 撤销已应用的武器加成（下场回收时调用，防止复用池对象时加成叠加）。
        /// </summary>
        public void RemoveAppliedModifiers()
        {
            if (Stats == null)
            {
                _appliedModifiers.Clear();
                return;
            }
            for (int i = 0; i < _appliedModifiers.Count; i++)
            {
                Stat stat = Stats.GetStat(_appliedModifiers[i].Type);
                stat?.RemoveModifier(_appliedModifiers[i].Value);
            }
            _appliedModifiers.Clear();
        }

        /// <summary>
        /// 标记死亡（幂等：仅首次生效）：
        /// 停止全部战斗组件。注册表移除与事件广播由 CombatManager 统一处理。
        /// </summary>
        /// <returns>true = 本次调用完成了标记；false = 已经死亡（重复调用）</returns>
        public bool MarkDead()
        {
            if (!IsAlive) return false;
            IsAlive = false;
            SetCombatComponentsEnabled(false);
            RemoveAppliedModifiers();
            return true;
        }

        /// <summary>
        /// 下场回收前的清理：停组件、撤销加成（存活状态保留，冷却后可再上场）。
        /// </summary>
        public void PrepareForRecall()
        {
            SetCombatComponentsEnabled(false);
            RemoveAppliedModifiers();
        }

        /// <summary>
        /// 统一开关战斗组件（死亡/下场停摆，上场恢复）。
        /// </summary>
        private void SetCombatComponentsEnabled(bool enabled)
        {
            if (Mover != null)
            {
                Mover.enabled = enabled;
                if (!enabled) Mover.Halt();
            }
            if (Targeting != null) Targeting.enabled = enabled;
            if (Attack != null) Attack.enabled = enabled;
            if (Charge != null) Charge.enabled = enabled;
            if (Caster != null) Caster.enabled = enabled;
        }
    }
}
