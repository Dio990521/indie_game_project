using System;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.ActionPoint
{
    /// <summary>
    /// 行动点系统（ActionPointSystem）：
    /// 维护玩家每回合可执行操作的次数上限。
    ///
    /// 设计说明：
    /// 1) 当前上限默认 10，后续培养系统可通过 SetMaxActionPoints 提升；
    /// 2) 消耗入口统一走 TryConsumeActionPoints，方便未来扩展扣减来源（道具/事件/debuff 等）；
    /// 3) 行为变化统一广播 ActionPointChangedEvent，UI 层解耦监听；
    /// 4) 接入 ISaveable，存档时保存当前点数与当前上限。
    /// </summary>
    public class ActionPointSystem : SaveableMonoSingleton<ActionPointSystem>
    {
        [Header("Config")]
        [Tooltip("初始行动点上限（未读档时生效）。")]
        [SerializeField] private int baseMaxActionPoints = 10;

        [Header("Runtime（只读）")]
        [Tooltip("当前剩余行动点（运行时显示，勿在 Inspector 直接修改）。")]
        [SerializeField] private int currentActionPoints;
        [Tooltip("当前行动点上限（可被培养提升）。")]
        [SerializeField] private int maxActionPoints;

        // 是否已完成初始化，防止重复覆盖数值。
        private bool _isInitialized;

        /// <summary> 存档模块唯一标识。</summary>
        public override string SaveID => "ActionPointSystem";

        /// <summary> 当前剩余行动点（只读）。</summary>
        public int CurrentActionPoints
        {
            get
            {
                EnsureInitialized();
                return currentActionPoints;
            }
        }

        /// <summary> 当前行动点上限（只读）。</summary>
        public int MaxActionPoints
        {
            get
            {
                EnsureInitialized();
                return maxActionPoints;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            EnsureInitialized();
            EnsureSaveRegistration(forceSearch: true);
        }

        // ──────────────────── 公开接口 ────────────────────

        /// <summary>
        /// 查询是否有足够行动点（纯判断，不改状态）。
        /// </summary>
        public bool CanConsume(int amount = 1)
        {
            EnsureInitialized();
            if (amount <= 0) return true;
            return currentActionPoints >= amount;
        }

        /// <summary>
        /// 尝试消耗行动点（原子判断 + 扣减）：
        /// - 返回 true：消耗成功；
        /// - 返回 false：点数不足或参数非法。
        /// 此接口为通用消耗入口，掷骰子、道具、事件等均走此处。
        /// </summary>
        /// <param name="amount">消耗数量（默认 1）。</param>
        /// <param name="reason">消耗原因，用于日志与 UI 区分来源。</param>
        public bool TryConsumeActionPoints(int amount = 1, string reason = null)
        {
            EnsureInitialized();

            if (amount == 0) return true;
            if (amount < 0) return false;
            if (currentActionPoints < amount) return false;

            currentActionPoints -= amount;
            RaiseActionPointChanged(-amount, NormalizeReason(reason, "TryConsumeActionPoints"));
            return true;
        }

        /// <summary>
        /// 恢复行动点（如翻回合、道具回复等）：
        /// 不会超过当前上限。
        /// </summary>
        /// <param name="amount">恢复数量。</param>
        /// <param name="reason">恢复原因。</param>
        public void RestoreActionPoints(int amount, string reason = null)
        {
            EnsureInitialized();
            if (amount <= 0) return;

            int before = currentActionPoints;
            currentActionPoints = Mathf.Min(currentActionPoints + amount, maxActionPoints);

            int delta = currentActionPoints - before;
            if (delta == 0) return;

            RaiseActionPointChanged(delta, NormalizeReason(reason, "RestoreActionPoints"));
        }

        /// <summary>
        /// 将当前行动点重置为满值（新回合刷新等场合使用）。
        /// </summary>
        public void RefillActionPoints(string reason = null)
        {
            EnsureInitialized();
            int before = currentActionPoints;
            currentActionPoints = maxActionPoints;
            int delta = currentActionPoints - before;
            if (delta == 0) return;
            RaiseActionPointChanged(delta, NormalizeReason(reason, "RefillActionPoints"));
        }

        /// <summary>
        /// 提升行动点上限（培养/装备解锁时调用）：
        /// - newMax 必须大于当前上限，否则忽略；
        /// - 提升上限后当前点数不自动补满，由调用方决定是否 Refill。
        /// </summary>
        /// <param name="newMax">新上限值。</param>
        /// <param name="reason">提升原因（如 "SkillUpgrade" / "EquipEffect"）。</param>
        public void SetMaxActionPoints(int newMax, string reason = null)
        {
            EnsureInitialized();
            if (newMax <= maxActionPoints)
            {
                DebugTools.LogWarning($"[ActionPointSystem] SetMaxActionPoints 忽略：新上限 {newMax} 不高于当前上限 {maxActionPoints}。");
                return;
            }

            maxActionPoints = newMax;
            // 当前点数可能因上限提升而需同步（此处保持不变，由外部决定是否 Refill）。
            RaiseActionPointChanged(0, NormalizeReason(reason, "SetMaxActionPoints"));
        }

        // ──────────────────── ISaveable ────────────────────

        /// <summary>
        /// 捕获存档状态。
        /// </summary>
        public override object CaptureState()
        {
            EnsureInitialized();
            return new ActionPointSaveState
            {
                CurrentActionPoints = currentActionPoints,
                MaxActionPoints = maxActionPoints
            };
        }

        /// <summary>
        /// 从存档恢复状态。
        /// </summary>
        public override void RestoreState(object data)
        {
            EnsureInitialized();
            if (!(data is ActionPointSaveState state)) return;

            int beforeCurrent = currentActionPoints;
            maxActionPoints = Mathf.Max(1, state.MaxActionPoints);
            currentActionPoints = Mathf.Clamp(state.CurrentActionPoints, 0, maxActionPoints);

            int delta = currentActionPoints - beforeCurrent;
            RaiseActionPointChanged(delta, "LoadRestore");
        }

        // ──────────────────── 内部工具 ────────────────────

        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            maxActionPoints = Mathf.Max(1, baseMaxActionPoints);
            currentActionPoints = maxActionPoints;
            _isInitialized = true;
            // 初始化后广播一次同步事件，供 UI 获取初始状态。
            RaiseActionPointChanged(0, "Initialize");
        }

        private void RaiseActionPointChanged(int delta, string reason)
        {
            DebugTools.Log($"[ActionPointSystem] 行动点变化：{delta:+#;-#;0}，当前 {currentActionPoints}/{maxActionPoints}，原因：{reason}");
            EventBus.Raise(new ActionPointChangedEvent
            {
                CurrentPoints = currentActionPoints,
                MaxPoints = maxActionPoints,
                Delta = delta,
                Reason = reason
            });
        }

        private static string NormalizeReason(string reason, string fallback)
        {
            return string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
        }

        /// <summary>
        /// 行动点系统存档结构。
        /// </summary>
        [Serializable]
        private class ActionPointSaveState
        {
            // 当前剩余行动点
            public int CurrentActionPoints;
            // 当前上限（培养后可能高于初始值）
            public int MaxActionPoints;
        }
    }
}
