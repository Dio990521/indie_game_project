using System;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Economy
{
    /// <summary>
    /// 金币系统（GoldSystem）：
    /// 当前阶段只提供最小可用能力：
    /// 1) 维护金币数值；
    /// 2) 提供 AddGold / TrySpendGold；
    /// 3) 在变化后广播 GoldChangedEvent；
    /// 4) 接入 ISaveable，实现存档与读档恢复。
    ///
    /// 架构定位：
    /// - 这是“数据与规则层”，不直接依赖任何 UI；
    /// - UI 或商店系统只通过公开接口和 EventBus 与它交互；
    /// - 保持简洁，后续再扩展多币种、交易日志、统计分析。
    /// </summary>
    public class GoldSystem : MonoSingleton<GoldSystem>, ISaveable
    {
        [Header("Config")]
        [Tooltip("首次初始化时的默认金币。仅在没有读档恢复时生效。")]
        [SerializeField] private int initialGold = 0;

        [Header("Runtime")]
        [Tooltip("当前金币（运行时）。调试可见，逻辑层通过 CurrentGold 只读访问。")]
        [SerializeField] private int currentGold;

        // 是否已执行初始化，避免重复初始化导致数值覆盖。
        private bool _isInitialized;
        // 存档系统引用缓存（延迟查找，减少重复搜索开销）。
        private SaveManager _saveManager;
        // 是否已经成功向 SaveManager 注册。
        private bool _isRegisteredToSaveManager;

        /// <summary>
        /// 存档模块唯一标识：
        /// SaveManager 通过该值把存档中的条目映射回本系统。
        /// </summary>
        public string SaveID => "GoldSystem";

        /// <summary>
        /// 当前金币只读访问器：
        /// 外部系统可以读取，但不应绕过 Add/TrySpend 直接写值。
        /// </summary>
        public int CurrentGold
        {
            get
            {
                EnsureInitialized();
                return currentGold;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            // 防止重复实例继续执行逻辑，确保只有保留的单例工作。
            if (Instance != this) return;

            EnsureInitialized();
            // Awake 里强制尝试一次注册，保证系统在最早阶段就可参与存档。
            EnsureSaveRegistration(forceSearch: true);
        }

        private void OnEnable()
        {
            // 生命周期恢复时再尝试一次注册（幂等），适配脚本重载/对象重启。
            EnsureSaveRegistration(forceSearch: false);
        }

        private void OnDisable()
        {
            // 对象不可用时注销注册，避免 SaveManager 持有失效引用。
            if (_isRegisteredToSaveManager && _saveManager != null)
            {
                _saveManager.Unregister(this);
            }
            _isRegisteredToSaveManager = false;
        }

        /// <summary>
        /// 增加金币：
        /// - amount <= 0 时忽略（避免错误调用污染事件）；
        /// - 内部做整型上限保护，防止溢出。
        /// </summary>
        public void AddGold(int amount, string reason = null)
        {
            EnsureInitialized();
            if (amount <= 0) return;

            int before = currentGold;
            long sum = (long)currentGold + amount;
            currentGold = sum > int.MaxValue ? int.MaxValue : (int)sum;

            int delta = currentGold - before;
            if (delta == 0) return;

            RaiseGoldChanged(delta, NormalizeReason(reason, "AddGold"));
        }

        /// <summary>
        /// 尝试消费金币（原子判断 + 扣减）：
        /// 返回值语义：
        /// - true：消费成功，已扣减；
        /// - false：消费失败（参数非法或余额不足）。
        /// </summary>
        public bool TrySpendGold(int cost, string reason = null)
        {
            EnsureInitialized();

            // 成本为 0 视为成功但不触发事件；负数视为非法输入直接失败。
            if (cost == 0) return true;
            if (cost < 0) return false;
            if (currentGold < cost) return false;

            currentGold -= cost;
            RaiseGoldChanged(-cost, NormalizeReason(reason, "TrySpendGold"));
            return true;
        }

        /// <summary>
        /// 查询是否可支付（纯判断，不改状态）。
        /// </summary>
        public bool CanAfford(int cost)
        {
            EnsureInitialized();
            if (cost <= 0) return true;
            return currentGold >= cost;
        }

        /// <summary>
        /// SaveManager 调用：捕获金币状态。
        /// </summary>
        public object CaptureState()
        {
            EnsureInitialized();
            return new GoldSaveState
            {
                CurrentGold = currentGold
            };
        }

        /// <summary>
        /// SaveManager 调用：恢复金币状态。
        /// </summary>
        public void RestoreState(object data)
        {
            EnsureInitialized();
            if (!(data is GoldSaveState state)) return;

            int before = currentGold;
            currentGold = Mathf.Max(0, state.CurrentGold);
            int delta = currentGold - before;

            // 读档后广播一次同步事件，让 UI 和依赖系统立即刷新。
            RaiseGoldChanged(delta, "LoadRestore");
        }

        /// <summary>
        /// 确保系统初始化：
        /// - 首次以 initialGold 建立运行时值；
        /// - 并广播一次 Delta=0 的同步事件，给 UI 提供初始渲染机会。
        /// </summary>
        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            currentGold = Mathf.Max(0, initialGold);
            _isInitialized = true;
            RaiseGoldChanged(0, "Initialize");
        }

        /// <summary>
        /// 广播金币变化事件（统一出口）：
        /// 所有变更路径都通过这里发事件，确保监听方行为一致。
        /// </summary>
        private void RaiseGoldChanged(int delta, string reason)
        {
            EventBus.Raise(new GoldChangedEvent
            {
                CurrentGold = currentGold,
                Delta = delta,
                Reason = reason
            });
        }

        /// <summary>
        /// 规范化原因字符串：
        /// 当调用方未传 reason 时，使用默认值，保证日志可读性。
        /// </summary>
        private static string NormalizeReason(string reason, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason.Trim();
            }
            return fallback;
        }

        /// <summary>
        /// 确保已向 SaveManager 注册。
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
        /// 使用场景查找避免对 SaveManager.Instance 的硬依赖日志。
        /// </summary>
        private SaveManager ResolveSaveManager(bool forceSearch)
        {
            if (_saveManager != null) return _saveManager;
            if (!forceSearch && _isRegisteredToSaveManager) return null;
            return FindAnyObjectByType<SaveManager>();
        }

        /// <summary>
        /// 金币系统存档结构：
        /// 当前只保存金币数值，后续可扩展（如交易计数/统计信息）。
        /// </summary>
        [Serializable]
        private class GoldSaveState
        {
            public int CurrentGold;
        }
    }
}
