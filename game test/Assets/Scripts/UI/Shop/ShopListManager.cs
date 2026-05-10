using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Shop;
using IndieGame.UI.Common;

namespace IndieGame.UI.Shop
{
    /// <summary>
    /// 商店界面左侧列表管理器（纯 C# 辅助类）：
    /// 职责：对象池管理、商品列表构建、条目索引、选中状态。
    /// 由 ShopUIController 在 Awake 中实例化并持有。
    /// 通用集合维护逻辑（_activeSlots / _entryByKey / _entryOrder / ReleaseAll）已迁移至 BaseListManager。
    /// </summary>
    internal class ShopListManager : BaseListManager<ShopListManager.ShopListEntry, ShopItemSlotUI>
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

        private ShopUIBinder _binder;

        // 避免每次重建都 new 的缓存
        private readonly List<ShopItemEntry> _shopEntryBuffer = new List<ShopItemEntry>();

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
        /// 尝试获取当前选中条目数据。
        /// </summary>
        public bool TryGetSelectedEntry(out ShopListEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(SelectedEntryKey)) return false;
            return _entryByKey.TryGetValue(SelectedEntryKey, out entry);
        }

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

        // --- 私有方法 ---

        private void AddEntry(ShopListEntry entry)
        {
            ShopItemSlotUI slotUI = SpawnSlot(entry);
            if (slotUI == null) return;
            // 父类登记三件套（_activeSlots / _entryByKey / _entryOrder）
            RegisterActiveSlot(entry.EntryKey, entry, slotUI);
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
