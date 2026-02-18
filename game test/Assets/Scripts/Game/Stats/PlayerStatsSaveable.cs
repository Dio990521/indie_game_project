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
    public class PlayerStatsSaveable : MonoBehaviour, ISaveable
    {
        [Header("Save Identity")]
        [Tooltip("该模块在存档中的唯一键。除非你明确要做断档迁移，否则不要修改。")]
        [SerializeField] private string saveID = "PlayerStats";

        private CharacterStats _stats;
        private SaveManager _saveManager;
        private bool _isRegisteredToSaveManager;

        // 当 RestoreState 发生时若 CharacterStats 尚不可用，先缓存到这里，待后续帧再应用。
        private PlayerStatsSaveState _pendingRestoreState;
        private bool _hasPendingRestoreState;

        /// <summary>
        /// SaveManager 用来匹配存档条目的唯一标识。
        /// </summary>
        public string SaveID => saveID;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        private void OnEnable()
        {
            // 关键设计：
            // 这里每次启用都尝试确保注册，原因是该组件可能经历“场景切换后重新启用”、
            // “脚本热重载”等生命周期变化。EnsureSaveRegistration 内部有幂等保护，不会重复注册。
            EnsureSaveRegistration(forceSearch: true);
            TryApplyPendingRestoreState();
        }

        private void OnDisable()
        {
            // 注意：这里故意“不注销 SaveManager 注册”。
            //
            // 背景问题：
            // - 在 Camp 场景中，玩家对象会被临时 SetActive(false) 来隐藏；
            // - 这会触发 OnDisable；
            // - 如果在 OnDisable 注销，则 Sleep 自动存档时 SaveManager 无法采集玩家 Stat。
            //
            // 因此策略改为：
            // - OnDisable 仅表示“暂时不可见/不可用”，不代表对象生命周期结束；
            // - 真正需要注销注册的时机放到 OnDestroy（对象销毁）里处理。
        }

        private void OnDestroy()
        {
            // 仅在对象真正销毁时注销，避免 SaveManager 持有无效模块引用。
            // 这样既能保证“临时隐藏仍可参与存档”，也能保证“对象销毁后不会脏引用累积”。
            if (_isRegisteredToSaveManager && _saveManager != null)
            {
                _saveManager.Unregister(this);
            }
            _isRegisteredToSaveManager = false;
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
        public object CaptureState()
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
        public void RestoreState(object data)
        {
            if (!(data is PlayerStatsSaveState state))
            {
                return;
            }

            if (!TryResolveStats(out CharacterStats stats))
            {
                _pendingRestoreState = state;
                _hasPendingRestoreState = true;
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
        /// 保证向 SaveManager 注册：
        /// - 若当前场景还没有 SaveManager，保留未注册状态，后续可再次尝试。
        /// </summary>
        private void EnsureSaveRegistration(bool forceSearch)
        {
            if (_isRegisteredToSaveManager) return;

            _saveManager = ResolveSaveManager(forceSearch);
            if (_saveManager == null) return;

            _saveManager.Register(this);
            _isRegisteredToSaveManager = true;
        }

        /// <summary>
        /// 查找 SaveManager：
        /// 使用场景搜索而非 SaveManager.Instance，避免在缺失时产生无意义警告日志。
        /// </summary>
        private SaveManager ResolveSaveManager(bool forceSearch)
        {
            if (_saveManager != null) return _saveManager;
            if (!forceSearch && _isRegisteredToSaveManager) return null;
            return FindAnyObjectByType<SaveManager>();
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
