using System;
using UnityEngine;
using IndieGame.Core.SaveSystem;

namespace IndieGame.Gameplay.Stats
{
    /// <summary>
    /// 玩家属性存档模块（ISaveable）：
    /// 仅负责“玩家 CharacterStats 的存取”，不处理 UI，不处理场景切换。
    ///
    /// 设计目标：
    /// 1) 把存档职责从 CharacterStats 中拆离，避免数值组件承担过多职责；
    /// 2) 与 SaveManager 解耦对接，后续扩展更多模块时沿用同一模式；
    /// 3) 支持“读档发生在玩家对象创建之前”的延迟恢复场景。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
    public class PlayerStatsSaveable : SaveableBehaviour
    {
        [Header("Save Identity")]
        [Tooltip("该模块在存档中的唯一键。除非你明确要做断档迁移，否则不要修改。")]
        [SerializeField] private string saveID = "PlayerStats";

        private CharacterStats _stats;

        // 当 RestoreState 发生时若 CharacterStats 尚不可用，先缓存到这里，待后续帧再应用。
        private PlayerStatsSaveState _pendingRestoreState;
        private bool _hasPendingRestoreState;

        /// <summary>
        /// SaveManager 用来匹配存档条目的唯一标识。
        /// </summary>
        public override string SaveID => saveID;

        /// <summary>
        /// 关键设计：玩家对象在 Camp 场景会被临时 SetActive(false)，
        /// 若在 OnDisable 注销则自动存档无法采集玩家数据。
        /// 因此覆盖为 false，依赖 SaveableBehaviour.OnDestroy 兜底注销。
        /// </summary>
        protected override bool UnregisterOnDisable => false;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        protected override void OnEnable()
        {
            // 父类完成 SaveManager 注册（幂等）；
            // 该组件可能经历“场景切换后重新启用”、“脚本热重载”等生命周期变化。
            base.OnEnable();
            TryApplyPendingRestoreState();

            // L4 修复：LateUpdate 只在"存在待应用的恢复数据"时才需要运行。
            // 注册已在上方完成（注销由 OnDestroy 兜底，UnregisterOnDisable=false），
            // 无 pending 时直接禁用组件，让引擎彻底跳过每帧回调。
            enabled = _hasPendingRestoreState;
        }

        private void LateUpdate()
        {
            // 若此前读档时玩家对象/属性尚未准备好，则在后续帧持续尝试应用一次。
            if (_hasPendingRestoreState)
            {
                TryApplyPendingRestoreState();
            }
        }

        /// <summary>
        /// 捕获当前玩家属性状态。
        /// </summary>
        public override object CaptureState()
        {
            if (!TryResolveStats(out CharacterStats stats))
            {
                return null;
            }

            return new PlayerStatsSaveState
            {
                CurrentHP = stats.CurrentHP,
                CurrentLevel = stats.CurrentLevel,
                CurrentEXP = stats.CurrentEXP
            };
        }

        /// <summary>
        /// 恢复玩家属性状态：
        /// - 正常情况：立即应用到 CharacterStats；
        /// - 目标未准备：先缓存，等待后续帧自动应用。
        /// </summary>
        public override void RestoreState(object data)
        {
            if (!(data is PlayerStatsSaveState state))
            {
                return;
            }

            if (!TryResolveStats(out CharacterStats stats))
            {
                _pendingRestoreState = state;
                _hasPendingRestoreState = true;
                // L4：出现待应用数据时恢复 LateUpdate 轮询
                enabled = true;
                return;
            }

            ApplyStateToStats(stats, state);
        }

        /// <summary>
        /// 尝试把缓存中的恢复数据应用到 CharacterStats。
        /// </summary>
        private void TryApplyPendingRestoreState()
        {
            if (!_hasPendingRestoreState) return;
            if (!TryResolveStats(out CharacterStats stats)) return;

            ApplyStateToStats(stats, _pendingRestoreState);
            _hasPendingRestoreState = false;
            _pendingRestoreState = null;
            // L4：pending 已消费完毕，停掉 LateUpdate 轮询
            enabled = false;
        }

        /// <summary>
        /// 真正执行数值写回，调用 CharacterStats 暴露的统一恢复入口。
        /// </summary>
        private static void ApplyStateToStats(CharacterStats stats, PlayerStatsSaveState state)
        {
            if (stats == null || state == null) return;

            stats.ApplySavedRuntimeState(
                state.CurrentHP,
                state.CurrentLevel,
                state.CurrentEXP
            );
        }

        /// <summary>
        /// 解析 CharacterStats 引用（带懒加载）。
        /// </summary>
        private bool TryResolveStats(out CharacterStats stats)
        {
            if (_stats == null)
            {
                _stats = GetComponent<CharacterStats>();
            }
            stats = _stats;
            return stats != null;
        }

        /// <summary>
        /// 玩家属性存档结构：
        /// 当前只保存最小可用集合（HP/Level/EXP），后续可按需追加字段。
        /// </summary>
        [Serializable]
        private class PlayerStatsSaveState
        {
            public int CurrentHP;
            public int CurrentLevel;
            public int CurrentEXP;
        }
    }
}
