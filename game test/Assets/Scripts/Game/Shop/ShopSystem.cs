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
    public class ShopSystem : SaveableMonoSingleton<ShopSystem>
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

        // 当前是否已初始化静态/运行时数据
        private bool _isInitialized;

        /// <summary>
        /// ISaveable 唯一标识。
        /// </summary>
        public override string SaveID => "ShopSystem";

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            EnsureInitialized();
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

            // 第一步：纯校验（不改任何状态）
            ShopPurchaseFailReason failReason = ValidatePurchase(
                shopId, shopEntryId, quantity,
                out int maxPurchasable,
                out ShopItemEntry entry,
                out RuntimeEntryState state);

            if (failReason != ShopPurchaseFailReason.None)
            {
                return BuildFailResult(failReason, maxPurchasable, ResolveFailMessage(failReason, maxPurchasable));
            }

            // 第二步：执行交易（扣金币 / 加物品 / 写库存 / 广播事件）
            return ExecutePurchase(shopId, shopEntryId, entry, state, quantity, maxPurchasable);
        }

        /// <summary>
        /// 校验阶段：依次检查商店/条目/数量/库存/金币/背包等约束。
        /// 整个流程只读不写，校验通过返回 None，否则返回具体失败原因。
        /// </summary>
        private ShopPurchaseFailReason ValidatePurchase(
            string shopId, string shopEntryId, int quantity,
            out int maxPurchasable,
            out ShopItemEntry entry,
            out RuntimeEntryState state)
        {
            maxPurchasable = 0;
            entry = null;
            state = null;

            if (string.IsNullOrWhiteSpace(shopId))
                return ShopPurchaseFailReason.InvalidShop;

            if (string.IsNullOrWhiteSpace(shopEntryId))
                return ShopPurchaseFailReason.InvalidEntry;

            if (!TryGetEntryAndState(shopId, shopEntryId, out entry, out state))
                return ShopPurchaseFailReason.InvalidEntry;

            if (quantity <= 0)
                return ShopPurchaseFailReason.InvalidQuantity;

            maxPurchasable = GetMaxPurchasableQuantity(shopId, shopEntryId);
            if (maxPurchasable <= 0)
                return ResolveBlockingReason(entry, state);

            if (quantity > maxPurchasable)
                return ShopPurchaseFailReason.AmountExceedsCurrentMax;

            if (GoldSystem.Instance == null)
                return ShopPurchaseFailReason.InsufficientGold;

            if (InventoryManager.Instance == null)
                return ShopPurchaseFailReason.InventoryFull;

            // 价格溢出预检查（int 乘法可能溢出，提前转 long 比较）
            long costLong = (long)entry.UnitPrice * quantity;
            if (costLong > int.MaxValue)
                return ShopPurchaseFailReason.CostOverflow;

            return ShopPurchaseFailReason.None;
        }

        /// <summary>
        /// 执行阶段：在校验通过的前提下做实际交易。
        /// 失败安全：扣金币后入包失败会自动退款回滚。
        /// </summary>
        private ShopPurchaseResult ExecutePurchase(
            string shopId, string shopEntryId,
            ShopItemEntry entry, RuntimeEntryState state,
            int quantity, int maxPurchasable)
        {
            int totalCost = entry.UnitPrice * quantity;
            GoldSystem goldSystem = GoldSystem.Instance;
            InventoryManager inventory = InventoryManager.Instance;

            if (!goldSystem.TrySpendGold(totalCost, "ShopPurchase"))
            {
                return BuildFailResult(ShopPurchaseFailReason.InsufficientGold, maxPurchasable, "金币不足。");
            }

            bool addSucceeded = inventory.AddItem(entry.Item, quantity);
            if (!addSucceeded)
            {
                // 退款回滚：
                // 理论上在 CanAddItem 的前置校验下不应失败；
                // 这里保留防御式回滚，避免未来其他系统并发改背包导致玩家资金损失。
                goldSystem.AddGold(totalCost, "ShopPurchaseRollback");
                return BuildFailResult(ShopPurchaseFailReason.InventoryFull, maxPurchasable, "背包空间不足，交易已自动回滚退款。");
            }

            // 正式写入运行时状态：有限库存才扣减；累计购买数始终增加（用于限购）。
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

            return new ShopPurchaseResult
            {
                Success = true,
                FailReason = ShopPurchaseFailReason.None,
                Message = "购买成功。",
                MaxPurchasableAtRequest = maxPurchasable,
                PurchasedQuantity = quantity,
                TotalCost = totalCost
            };
        }

        /// <summary>
        /// 构造统一格式的失败结果。
        /// </summary>
        private static ShopPurchaseResult BuildFailResult(ShopPurchaseFailReason reason, int maxPurchasable, string message)
        {
            return new ShopPurchaseResult
            {
                Success = false,
                FailReason = reason,
                Message = message ?? string.Empty,
                MaxPurchasableAtRequest = maxPurchasable,
                PurchasedQuantity = 0,
                TotalCost = 0
            };
        }

        /// <summary>
        /// 把校验失败原因映射到面向玩家的中文提示。
        /// </summary>
        private static string ResolveFailMessage(ShopPurchaseFailReason reason, int maxPurchasable)
        {
            switch (reason)
            {
                case ShopPurchaseFailReason.InvalidShop:           return "商店 ID 无效。";
                case ShopPurchaseFailReason.InvalidEntry:          return "找不到对应商品条目。";
                case ShopPurchaseFailReason.InvalidQuantity:       return "购买数量必须大于 0。";
                case ShopPurchaseFailReason.NoStock:               return "当前条件下无法购买该商品。";
                case ShopPurchaseFailReason.PurchaseLimitReached:  return "当前条件下无法购买该商品。";
                case ShopPurchaseFailReason.InsufficientGold:      return "当前条件下无法购买该商品。";
                case ShopPurchaseFailReason.InventoryFull:         return "当前条件下无法购买该商品。";
                case ShopPurchaseFailReason.AmountExceedsCurrentMax:
                    return $"超过可购买上限，当前最多可买 {maxPurchasable} 个。";
                case ShopPurchaseFailReason.CostOverflow:          return "本次交易金额过大。";
                default:                                           return string.Empty;
            }
        }

        /// <summary>
        /// 保存运行时状态。
        /// </summary>
        public override object CaptureState()
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
        public override void RestoreState(object data)
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
        /// 商店主索引复用 DatabaseIndexer，每个商店内部条目再单独索引一次。
        /// </summary>
        private void RebuildStaticIndex()
        {
            _shopById.Clear();
            _entryByShopId.Clear();

            if (shopDatabase == null || shopDatabase.Shops == null)
            {
                DebugTools.LogWarning("[ShopSystem] Missing ShopDatabaseSO.");
                return;
            }

            // 复用通用索引器构建“ShopID -> ShopSO”
            Dictionary<string, ShopSO> shopMap = DatabaseIndexer.BuildById(
                shopDatabase.Shops,
                shop => shop != null ? shop.ID : null,
                "ShopSystem");

            foreach (KeyValuePair<string, ShopSO> pair in shopMap)
            {
                _shopById.Add(pair.Key, pair.Value);

                Dictionary<string, ShopItemEntry> entryMap = DatabaseIndexer.BuildById(
                    pair.Value.Entries,
                    entry => (entry != null && entry.Item != null) ? entry.EntryID : null,
                    $"ShopSystem:{pair.Key}");

                _entryByShopId.Add(pair.Key, entryMap);
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
