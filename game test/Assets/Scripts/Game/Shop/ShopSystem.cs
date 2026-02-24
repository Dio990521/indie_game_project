using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Economy;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Gameplay.Shop
{
    /// <summary>
    /// 商店购买失败原因枚举：
    /// 用于把失败结果结构化，避免 UI 只能依赖字符串判断。
    /// </summary>
    public enum ShopPurchaseFailReason
    {
        None = 0,
        InvalidShop = 1,
        InvalidEntry = 2,
        InvalidQuantity = 3,
        NoStock = 4,
        PurchaseLimitReached = 5,
        InsufficientGold = 6,
        InventoryFull = 7,
        AmountExceedsCurrentMax = 8,
        CostOverflow = 9
    }

    /// <summary>
    /// 商店购买结果：
    /// 统一封装交易执行后的成功/失败状态与上下文信息。
    /// </summary>
    [Serializable]
    public struct ShopPurchaseResult
    {
        // 是否成功
        public bool Success;
        // 失败原因（Success=true 时为 None）
        public ShopPurchaseFailReason FailReason;
        // 结果提示（可用于日志或 UI Toast）
        public string Message;
        // 请求时的可购买上限（便于 UI 反馈“最多可买 N 个”）
        public int MaxPurchasableAtRequest;
        // 实际购买数量（成功时 >0，失败时为 0）
        public int PurchasedQuantity;
        // 实际总花费（成功时 >0，失败时为 0）
        public int TotalCost;
    }

    /// <summary>
    /// 商店系统（逻辑/数据层）：
    /// 职责边界：
    /// 1) 维护商店静态配置索引（ShopDatabaseSO -> Dictionary）；
    /// 2) 维护运行时动态状态（库存剩余、累计购买数）；
    /// 3) 执行交易规则（库存、限购、金币、背包容量）；
    /// 4) 对接存档（ISaveable），保证库存/限购可跨读档恢复。
    ///
    /// 性能策略：
    /// - 逻辑层全部走 Dictionary 索引；
    /// - 高频查询不做 List 全扫描，避免 UI 刷新时的额外开销。
    /// </summary>
    public class ShopSystem : MonoSingleton<ShopSystem>, ISaveable
    {
        [Header("Config")]
        [Tooltip("商店数据库（静态数据）。")]
        [SerializeField] private ShopDatabaseSO shopDatabase;

        // --- 静态索引 ---
        // ShopID -> ShopSO
        private readonly Dictionary<string, ShopSO> _shopById = new Dictionary<string, ShopSO>(StringComparer.Ordinal);
        // ShopID -> (ShopEntryID -> ShopItemEntry)
        private readonly Dictionary<string, Dictionary<string, ShopItemEntry>> _entryByShopId =
            new Dictionary<string, Dictionary<string, ShopItemEntry>>(StringComparer.Ordinal);

        // --- 运行时状态 ---
        // ShopID -> (ShopEntryID -> RuntimeEntryState)
        private readonly Dictionary<string, Dictionary<string, RuntimeEntryState>> _runtimeByShopId =
            new Dictionary<string, Dictionary<string, RuntimeEntryState>>(StringComparer.Ordinal);

        // 存档系统缓存
        private SaveManager _saveManager;
        private bool _isRegisteredToSaveManager;
        private bool _isInitialized;

        /// <summary>
        /// ISaveable 唯一标识。
        /// </summary>
        public string SaveID => "ShopSystem";

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureSaveRegistration(forceSearch: false);
        }

        private void OnDisable()
        {
            if (_isRegisteredToSaveManager && _saveManager != null)
            {
                _saveManager.Unregister(this);
            }
            _isRegisteredToSaveManager = false;
        }

        /// <summary>
        /// 获取商店配置。
        /// </summary>
        public ShopSO GetShop(string shopId)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(shopId)) return null;
            _shopById.TryGetValue(shopId.Trim(), out ShopSO shop);
            return shop;
        }

        /// <summary>
        /// 获取商店条目列表（按 ShopSO 配置顺序输出）：
        /// UI 层使用该方法构建左侧商品列表，保证显示顺序稳定。
        /// </summary>
        public void GetEntries(string shopId, List<ShopItemEntry> output)
        {
            if (output == null) return;
            output.Clear();

            EnsureInitialized();
            ShopSO shop = GetShop(shopId);
            if (shop == null || shop.Entries == null) return;

            for (int i = 0; i < shop.Entries.Count; i++)
            {
                ShopItemEntry entry = shop.Entries[i];
                if (entry == null) continue;
                if (entry.Item == null) continue;
                if (string.IsNullOrWhiteSpace(entry.EntryID)) continue;
                output.Add(entry);
            }
        }

        /// <summary>
        /// 获取某条目的剩余库存：
        /// -1 = 无限库存。
        /// </summary>
        public int GetRemainingStock(string shopId, string shopEntryId)
        {
            if (!TryGetEntryAndState(shopId, shopEntryId, out _, out RuntimeEntryState state))
            {
                return 0;
            }
            return state.RemainingStock;
        }

        /// <summary>
        /// 获取某条目的剩余可购配额：
        /// -1 = 无限购买。
        /// </summary>
        public int GetRemainingPurchaseQuota(string shopId, string shopEntryId)
        {
            if (!TryGetEntryAndState(shopId, shopEntryId, out ShopItemEntry entry, out RuntimeEntryState state))
            {
                return 0;
            }

            if (entry.PurchaseLimitPerSave < 0) return -1;
            return Mathf.Max(0, entry.PurchaseLimitPerSave - state.PurchasedCount);
        }

        /// <summary>
        /// 获取“当前上下文下可购买最大数量”：
        /// 同时考虑以下限制：
        /// 1) 库存上限
        /// 2) 限购上限
        /// 3) 金币可支付上限
        /// 4) 背包可容纳上限
        /// </summary>
        public int GetMaxPurchasableQuantity(string shopId, string shopEntryId)
        {
            if (!TryGetEntryAndState(shopId, shopEntryId, out ShopItemEntry entry, out RuntimeEntryState state))
            {
                return 0;
            }

            if (entry.Item == null) return 0;

            GoldSystem goldSystem = GoldSystem.Instance;
            InventoryManager inventory = InventoryManager.Instance;
            if (goldSystem == null || inventory == null) return 0;

            int stockCap = state.RemainingStock < 0 ? int.MaxValue : Mathf.Max(0, state.RemainingStock);
            int quotaCap = entry.PurchaseLimitPerSave < 0
                ? int.MaxValue
                : Mathf.Max(0, entry.PurchaseLimitPerSave - state.PurchasedCount);
            int goldCap = entry.UnitPrice <= 0 ? 0 : Mathf.Max(0, goldSystem.CurrentGold / entry.UnitPrice);

            int upperBound = Mathf.Min(stockCap, Mathf.Min(quotaCap, goldCap));
            if (upperBound <= 0) return 0;

            // 背包容量约束：
            // 由于 InventoryManager.CanAddItem(amount) 对 amount 单调（数量越大越难放下），
            // 这里使用二分查找获取“可完整加入的最大数量”，避免线性试探导致 O(N)。
            return ResolveMaxFittableByInventory(inventory, entry.Item, upperBound);
        }

        /// <summary>
        /// 执行购买：
        /// 成功顺序：
        /// 1) 校验可购上限
        /// 2) 扣金币
        /// 3) 加物品
        /// 4) 扣库存/累加已购
        /// 5) 广播购买成功事件
        ///
        /// 失败安全：
        /// - 若扣金币后入包失败，会执行退款回滚，避免“扣钱但没拿到物品”。
        /// </summary>
        public ShopPurchaseResult TryPurchase(string shopId, string shopEntryId, int quantity)
        {
            EnsureInitialized();

            ShopPurchaseResult result = new ShopPurchaseResult
            {
                Success = false,
                FailReason = ShopPurchaseFailReason.None,
                Message = string.Empty,
                MaxPurchasableAtRequest = 0,
                PurchasedQuantity = 0,
                TotalCost = 0
            };

            if (string.IsNullOrWhiteSpace(shopId))
            {
                result.FailReason = ShopPurchaseFailReason.InvalidShop;
                result.Message = "商店 ID 无效。";
                return result;
            }

            if (string.IsNullOrWhiteSpace(shopEntryId))
            {
                result.FailReason = ShopPurchaseFailReason.InvalidEntry;
                result.Message = "商品条目 ID 无效。";
                return result;
            }

            if (!TryGetEntryAndState(shopId, shopEntryId, out ShopItemEntry entry, out RuntimeEntryState state))
            {
                result.FailReason = ShopPurchaseFailReason.InvalidEntry;
                result.Message = "找不到对应商品条目。";
                return result;
            }

            if (quantity <= 0)
            {
                result.FailReason = ShopPurchaseFailReason.InvalidQuantity;
                result.Message = "购买数量必须大于 0。";
                return result;
            }

            int maxPurchasable = GetMaxPurchasableQuantity(shopId, shopEntryId);
            result.MaxPurchasableAtRequest = maxPurchasable;
            if (maxPurchasable <= 0)
            {
                result.FailReason = ResolveBlockingReason(entry, state);
                result.Message = "当前条件下无法购买该商品。";
                return result;
            }

            if (quantity > maxPurchasable)
            {
                result.FailReason = ShopPurchaseFailReason.AmountExceedsCurrentMax;
                result.Message = $"超过可购买上限，当前最多可买 {maxPurchasable} 个。";
                return result;
            }

            GoldSystem goldSystem = GoldSystem.Instance;
            InventoryManager inventory = InventoryManager.Instance;
            if (goldSystem == null)
            {
                result.FailReason = ShopPurchaseFailReason.InsufficientGold;
                result.Message = "金币系统不可用。";
                return result;
            }

            if (inventory == null)
            {
                result.FailReason = ShopPurchaseFailReason.InventoryFull;
                result.Message = "背包系统不可用。";
                return result;
            }

            long costLong = (long)entry.UnitPrice * quantity;
            if (costLong > int.MaxValue)
            {
                result.FailReason = ShopPurchaseFailReason.CostOverflow;
                result.Message = "本次交易金额过大。";
                return result;
            }

            int totalCost = (int)costLong;
            if (!goldSystem.TrySpendGold(totalCost, "ShopPurchase"))
            {
                result.FailReason = ShopPurchaseFailReason.InsufficientGold;
                result.Message = "金币不足。";
                return result;
            }

            bool addSucceeded = inventory.AddItem(entry.Item, quantity);
            if (!addSucceeded)
            {
                // 退款回滚：
                // 理论上在 CanAddItem 的前置校验下不应失败；
                // 这里保留防御式回滚，避免未来其他系统并发改背包导致玩家资金损失。
                goldSystem.AddGold(totalCost, "ShopPurchaseRollback");

                result.FailReason = ShopPurchaseFailReason.InventoryFull;
                result.Message = "背包空间不足，交易已自动回滚退款。";
                return result;
            }

            // 正式写入运行时状态：
            // 1) 有限库存才扣减；
            // 2) 累计购买数始终增加（用于限购）。
            if (state.RemainingStock >= 0)
            {
                state.RemainingStock = Mathf.Max(0, state.RemainingStock - quantity);
            }
            state.PurchasedCount = Mathf.Max(0, state.PurchasedCount + quantity);

            EventBus.Raise(new ShopPurchaseCompletedEvent
            {
                ShopID = shopId,
                ShopEntryID = shopEntryId,
                Quantity = quantity,
                TotalCost = totalCost
            });

            result.Success = true;
            result.FailReason = ShopPurchaseFailReason.None;
            result.Message = "购买成功。";
            result.PurchasedQuantity = quantity;
            result.TotalCost = totalCost;
            return result;
        }

        /// <summary>
        /// 保存运行时状态。
        /// </summary>
        public object CaptureState()
        {
            EnsureInitialized();

            ShopSystemSaveState state = new ShopSystemSaveState();
            foreach (KeyValuePair<string, Dictionary<string, RuntimeEntryState>> shopPair in _runtimeByShopId)
            {
                string shopId = shopPair.Key;
                Dictionary<string, RuntimeEntryState> runtimeMap = shopPair.Value;
                if (runtimeMap == null) continue;

                foreach (KeyValuePair<string, RuntimeEntryState> entryPair in runtimeMap)
                {
                    RuntimeEntryState runtime = entryPair.Value;
                    if (runtime == null) continue;

                    state.Entries.Add(new ShopRuntimeEntrySaveData
                    {
                        ShopID = shopId,
                        ShopEntryID = entryPair.Key,
                        RemainingStock = runtime.RemainingStock,
                        PurchasedCount = runtime.PurchasedCount
                    });
                }
            }

            return state;
        }

        /// <summary>
        /// 恢复运行时状态：
        /// 先重建默认状态，再覆盖存档值，确保：
        /// - 新增条目能自动带默认值；
        /// - 已删除条目不会污染当前运行时数据。
        /// </summary>
        public void RestoreState(object data)
        {
            EnsureInitialized();
            ResetRuntimeStateToDefaults();

            if (!(data is ShopSystemSaveState state) || state.Entries == null) return;

            for (int i = 0; i < state.Entries.Count; i++)
            {
                ShopRuntimeEntrySaveData saved = state.Entries[i];
                if (saved == null) continue;
                if (string.IsNullOrWhiteSpace(saved.ShopID) || string.IsNullOrWhiteSpace(saved.ShopEntryID)) continue;

                if (!TryGetEntryAndState(saved.ShopID, saved.ShopEntryID, out ShopItemEntry entry, out RuntimeEntryState runtime))
                {
                    continue;
                }

                // 读取时做边界保护，防止脏存档导致负值污染。
                runtime.PurchasedCount = Mathf.Max(0, saved.PurchasedCount);
                if (entry.InitialStock < 0)
                {
                    runtime.RemainingStock = -1;
                }
                else
                {
                    runtime.RemainingStock = Mathf.Clamp(saved.RemainingStock, 0, entry.InitialStock);
                }
            }
        }

        /// <summary>
        /// 确保系统初始化。
        /// </summary>
        private void EnsureInitialized()
        {
            if (_isInitialized) return;
            RebuildStaticIndex();
            ResetRuntimeStateToDefaults();
            EnsureSaveRegistration(forceSearch: true);
            _isInitialized = true;
        }

        /// <summary>
        /// 数据库索引构建（List -> Dictionary）。
        /// </summary>
        private void RebuildStaticIndex()
        {
            _shopById.Clear();
            _entryByShopId.Clear();

            if (shopDatabase == null || shopDatabase.Shops == null)
            {
                Debug.LogWarning("[ShopSystem] Missing ShopDatabaseSO.");
                return;
            }

            for (int i = 0; i < shopDatabase.Shops.Count; i++)
            {
                ShopSO shop = shopDatabase.Shops[i];
                if (shop == null) continue;
                if (string.IsNullOrWhiteSpace(shop.ID))
                {
                    Debug.LogWarning("[ShopSystem] Shop has empty ID, ignored.");
                    continue;
                }

                if (_shopById.ContainsKey(shop.ID))
                {
                    Debug.LogWarning($"[ShopSystem] Duplicate Shop ID ignored: {shop.ID}");
                    continue;
                }

                _shopById.Add(shop.ID, shop);

                Dictionary<string, ShopItemEntry> entryMap = new Dictionary<string, ShopItemEntry>(StringComparer.Ordinal);
                _entryByShopId.Add(shop.ID, entryMap);

                if (shop.Entries == null) continue;
                for (int entryIndex = 0; entryIndex < shop.Entries.Count; entryIndex++)
                {
                    ShopItemEntry entry = shop.Entries[entryIndex];
                    if (entry == null) continue;
                    if (entry.Item == null) continue;
                    if (string.IsNullOrWhiteSpace(entry.EntryID))
                    {
                        Debug.LogWarning($"[ShopSystem] Shop entry has empty ID. Shop={shop.ID}, Index={entryIndex}");
                        continue;
                    }

                    if (entryMap.ContainsKey(entry.EntryID))
                    {
                        Debug.LogWarning($"[ShopSystem] Duplicate shop entry ignored. Shop={shop.ID}, EntryID={entry.EntryID}");
                        continue;
                    }

                    entryMap.Add(entry.EntryID, entry);
                }
            }
        }

        /// <summary>
        /// 将运行时状态重置为“配置初始值”。
        /// </summary>
        private void ResetRuntimeStateToDefaults()
        {
            _runtimeByShopId.Clear();

            foreach (KeyValuePair<string, Dictionary<string, ShopItemEntry>> shopPair in _entryByShopId)
            {
                Dictionary<string, RuntimeEntryState> runtimeMap = new Dictionary<string, RuntimeEntryState>(StringComparer.Ordinal);
                _runtimeByShopId.Add(shopPair.Key, runtimeMap);

                foreach (KeyValuePair<string, ShopItemEntry> entryPair in shopPair.Value)
                {
                    ShopItemEntry entry = entryPair.Value;
                    if (entry == null) continue;

                    runtimeMap.Add(entryPair.Key, new RuntimeEntryState
                    {
                        RemainingStock = entry.InitialStock < 0 ? -1 : Mathf.Max(0, entry.InitialStock),
                        PurchasedCount = 0
                    });
                }
            }
        }

        /// <summary>
        /// 获取条目配置与运行时状态（双索引命中）。
        /// </summary>
        private bool TryGetEntryAndState(string shopId, string shopEntryId, out ShopItemEntry entry, out RuntimeEntryState state)
        {
            EnsureInitialized();

            entry = null;
            state = null;

            if (string.IsNullOrWhiteSpace(shopId) || string.IsNullOrWhiteSpace(shopEntryId)) return false;

            if (!_entryByShopId.TryGetValue(shopId.Trim(), out Dictionary<string, ShopItemEntry> entryMap)) return false;
            if (!_runtimeByShopId.TryGetValue(shopId.Trim(), out Dictionary<string, RuntimeEntryState> runtimeMap)) return false;

            string normalizedEntryId = shopEntryId.Trim();
            if (!entryMap.TryGetValue(normalizedEntryId, out entry)) return false;
            if (!runtimeMap.TryGetValue(normalizedEntryId, out state)) return false;

            return true;
        }

        /// <summary>
        /// 根据当前上下文推导“不能购买”的主阻塞原因。
        /// </summary>
        private ShopPurchaseFailReason ResolveBlockingReason(ShopItemEntry entry, RuntimeEntryState state)
        {
            if (entry == null || state == null) return ShopPurchaseFailReason.InvalidEntry;

            if (state.RemainingStock == 0) return ShopPurchaseFailReason.NoStock;

            if (entry.PurchaseLimitPerSave >= 0 && state.PurchasedCount >= entry.PurchaseLimitPerSave)
            {
                return ShopPurchaseFailReason.PurchaseLimitReached;
            }

            GoldSystem goldSystem = GoldSystem.Instance;
            if (goldSystem == null || goldSystem.CurrentGold < entry.UnitPrice)
            {
                return ShopPurchaseFailReason.InsufficientGold;
            }

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null || !inventory.CanAddItem(entry.Item, 1))
            {
                return ShopPurchaseFailReason.InventoryFull;
            }

            return ShopPurchaseFailReason.AmountExceedsCurrentMax;
        }

        /// <summary>
        /// 在 [1, upperBound] 区间内，用二分查找计算“背包可完整容纳”的最大数量。
        /// </summary>
        private static int ResolveMaxFittableByInventory(InventoryManager inventory, ItemSO item, int upperBound)
        {
            if (inventory == null || item == null || upperBound <= 0) return 0;

            int left = 1;
            int right = upperBound;
            int best = 0;

            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                if (inventory.CanAddItem(item, mid))
                {
                    best = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return best;
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
        /// 解析 SaveManager（避免硬依赖 Instance 触发日志）。
        /// </summary>
        private SaveManager ResolveSaveManager(bool forceSearch)
        {
            if (_saveManager != null) return _saveManager;
            if (!forceSearch && _isRegisteredToSaveManager) return null;
            return FindAnyObjectByType<SaveManager>();
        }

        /// <summary>
        /// 单条商品运行时状态。
        /// </summary>
        [Serializable]
        private class RuntimeEntryState
        {
            // 剩余库存：-1 代表无限库存
            public int RemainingStock;
            // 累计已购数量（用于限购）
            public int PurchasedCount;
        }

        /// <summary>
        /// 商店系统存档结构。
        /// </summary>
        [Serializable]
        private class ShopSystemSaveState
        {
            public List<ShopRuntimeEntrySaveData> Entries = new List<ShopRuntimeEntrySaveData>();
        }

        /// <summary>
        /// 单条商品存档结构。
        /// </summary>
        [Serializable]
        private class ShopRuntimeEntrySaveData
        {
            public string ShopID;
            public string ShopEntryID;
            public int RemainingStock;
            public int PurchasedCount;
        }
    }
}
