using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Shop;

namespace IndieGame.UI.Shop
{
    /// <summary>
    /// 商店界面左侧列表管理器（纯 C# 辅助类）：
    /// 职责：对象池管理、商品列表构建、条目索引、选中状态。
    /// 由 ShopUIController 在 Awake 中实例化并持有。
    /// </summary>
    internal class ShopListManager
    {
        // 列表条目 UI 适配模型：将 ShopSystem 数据映射到统一展示结构
        internal struct ShopListEntry
        {
            public string EntryKey;
            public string ShopID;
            public string ShopEntryID;
            public string DisplayName;
            public string Description;
            public Sprite Icon;
            public int UnitPrice;
        }

        private GameObjectPool _slotPool;
        private ShopUIBinder _binder;

        private readonly List<ShopItemSlotUI> _activeSlots = new List<ShopItemSlotUI>();
        private readonly Dictionary<string, ShopListEntry> _entryByKey = new Dictionary<string, ShopListEntry>(StringComparer.Ordinal);
        private readonly List<string> _entryOrder = new List<string>();

        // 避免每次重建都 new 的缓存
        private readonly List<ShopItemEntry> _shopEntryBuffer = new List<ShopItemEntry>();

        public string SelectedEntryKey { get; private set; }
        public IReadOnlyList<string> EntryOrder => _entryOrder;

        /// <summary>
        /// 初始化：创建对象池，必须在 Awake 中调用。
        /// </summary>
        public void Init(ShopUIBinder binder, int slotPoolWarmup)
        {
            _binder = binder;
            if (binder.SlotPrefab != null && binder.ListRoot != null)
                _slotPool = new GameObjectPool(binder.SlotPrefab, binder.ListRoot, slotPoolWarmup);
        }

        /// <summary>
        /// 从 ShopSystem 重建商品列表。返回是否有条目可用。
        /// </summary>
        public bool RebuildList(string shopId, ShopSystem shopSystem)
        {
            ReleaseAll();
            if (string.IsNullOrWhiteSpace(shopId) || shopSystem == null) return false;

            shopSystem.GetEntries(shopId, _shopEntryBuffer);
            for (int i = 0; i < _shopEntryBuffer.Count; i++)
            {
                ShopItemEntry entry = _shopEntryBuffer[i];
                if (entry == null || entry.Item == null) continue;

                string entryKey   = $"SHOP:{i}:{entry.EntryID}";
                string displayName = ResolveItemDisplayName(entry.Item);
                string description = string.IsNullOrWhiteSpace(entry.Item.Description)
                    ? "No description available."
                    : entry.Item.Description;

                AddEntry(new ShopListEntry
                {
                    EntryKey     = entryKey,
                    ShopID       = shopId,
                    ShopEntryID  = entry.EntryID,
                    DisplayName  = displayName,
                    Description  = description,
                    Icon         = entry.Item.Icon,
                    UnitPrice    = entry.UnitPrice
                });
            }
            return _entryOrder.Count > 0;
        }

        /// <summary>
        /// 记录当前选中条目。
        /// </summary>
        public void Select(string entryKey)
        {
            if (_entryByKey.ContainsKey(entryKey))
                SelectedEntryKey = entryKey;
        }

        /// <summary>
        /// 尝试获取当前选中条目数据。
        /// </summary>
        public bool TryGetSelectedEntry(out ShopListEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(SelectedEntryKey)) return false;
            return _entryByKey.TryGetValue(SelectedEntryKey, out entry);
        }

        /// <summary>
        /// 尝试通过指定 key 获取条目数据。
        /// </summary>
        public bool TryGetEntry(string key, out ShopListEntry entry) =>
            _entryByKey.TryGetValue(key, out entry);

        /// <summary>
        /// 在当前列表中查找指定 ShopEntryID 对应的 EntryKey。
        /// </summary>
        public string FindEntryKeyByShopEntryId(string shopEntryId)
        {
            if (string.IsNullOrWhiteSpace(shopEntryId)) return string.Empty;
            string target = shopEntryId.Trim();

            for (int i = 0; i < _entryOrder.Count; i++)
            {
                if (!_entryByKey.TryGetValue(_entryOrder[i], out ShopListEntry e)) continue;
                if (string.Equals(e.ShopEntryID, target, StringComparison.Ordinal)) return _entryOrder[i];
            }
            return string.Empty;
        }

        /// <summary>
        /// 回收所有列表项并清空索引。
        /// </summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                if (_activeSlots[i] != null && _slotPool != null)
                    _slotPool.Release(_activeSlots[i].gameObject);
            }
            _activeSlots.Clear();
            _entryByKey.Clear();
            _entryOrder.Clear();
            SelectedEntryKey = string.Empty;
        }

        // --- 私有方法 ---

        private void AddEntry(ShopListEntry entry)
        {
            ShopItemSlotUI slotUI = SpawnSlot(entry);
            if (slotUI == null) return;

            _activeSlots.Add(slotUI);
            _entryByKey[entry.EntryKey] = entry;
            _entryOrder.Add(entry.EntryKey);
        }

        private ShopItemSlotUI SpawnSlot(ShopListEntry entry)
        {
            if (_slotPool == null || _binder.ListRoot == null) return null;

            GameObject go = _slotPool.Get();
            go.transform.SetParent(_binder.ListRoot, false);

            ShopItemSlotUI slotUI = go.GetComponent<ShopItemSlotUI>();
            if (slotUI == null)
            {
                DebugTools.LogError("[ShopListManager] Slot prefab 缺少 ShopItemSlotUI 组件。");
                _slotPool.Release(go);
                return null;
            }

            slotUI.Setup(entry.EntryKey, entry.ShopID, entry.ShopEntryID, entry.Icon, entry.DisplayName, entry.UnitPrice);
            return slotUI;
        }

        private static string ResolveItemDisplayName(IndieGame.Gameplay.Inventory.ItemSO item)
        {
            if (item == null) return "Unknown Item";

            if (item.ItemName != null)
            {
                string localized = item.ItemName.GetLocalizedString();
                if (!string.IsNullOrWhiteSpace(localized)) return localized;
            }

            if (!string.IsNullOrWhiteSpace(item.ID)) return item.ID.Trim();
            return "Unknown Item";
        }
    }
}
