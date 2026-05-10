using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.UI.Common;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 打造界面左侧列表管理器（纯 C# 辅助类，无 MonoBehaviour 生命周期）：
    /// 职责：对象池管理、列表构建（原型/复现两种数据源）、条目索引、选中状态。
    /// 由 CraftingUIController 在 Awake 中实例化并持有。
    /// 通用集合维护逻辑（_activeSlots / _entryByKey / _entryOrder / ReleaseAll）已迁移至 BaseListManager。
    /// </summary>
    internal class CraftingListManager : BaseListManager<CraftingListManager.CraftListEntry, BlueprintSlotUI>
    {
        // 列表条目 UI 适配模型：将不同数据源统一映射到同一 UI 槽位
        internal struct CraftListEntry
        {
            public string EntryKey;
            public string BlueprintID;
            public string DisplayName;
            public string SuggestedName;
        }

        private CraftUIBinder _binder;

        // 单独维护“EntryKey -> Slot”，用于按蓝图 ID 精确移除单个条目
        private readonly Dictionary<string, BlueprintSlotUI> _slotByEntryKey = new Dictionary<string, BlueprintSlotUI>(StringComparer.Ordinal);

        // 避免每次重建列表都 new 的临时缓存
        private readonly List<BlueprintRecord> _recordsCache = new List<BlueprintRecord>();
        private readonly List<CraftHistoryEntry> _historyCache = new List<CraftHistoryEntry>();
        private readonly HashSet<string> _dedupeSet = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 初始化：创建对象池，必须在 Awake 中调用。
        /// </summary>
        public void Init(CraftUIBinder binder, int slotPoolWarmup)
        {
            _binder = binder;
            if (binder.SlotPrefab != null && binder.ListRoot != null)
                _slotPool = new GameObjectPool(binder.SlotPrefab, binder.ListRoot, slotPoolWarmup);
        }

        /// <summary>
        /// 重建"原型制造"列表（数据源：未消耗蓝图记录）。
        /// 返回是否存在条目。
        /// </summary>
        public bool RebuildForPrototype(CraftingSystem craftingSystem)
        {
            ReleaseAll();
            if (craftingSystem == null) return false;

            craftingSystem.GetAvailableBlueprintRecords(_recordsCache);
            for (int i = 0; i < _recordsCache.Count; i++)
            {
                BlueprintRecord record = _recordsCache[i];
                if (record == null || string.IsNullOrWhiteSpace(record.ID)) continue;

                BlueprintSO blueprint = craftingSystem.GetBlueprint(record.ID);
                if (blueprint == null) continue;

                string origName = craftingSystem.GetOriginalProductName(blueprint);
                string displayName = string.IsNullOrWhiteSpace(origName) ? blueprint.DefaultName : origName;

                AddEntry(new CraftListEntry
                {
                    EntryKey     = $"P:{record.ID}",
                    BlueprintID  = blueprint.ID,
                    DisplayName  = displayName,
                    SuggestedName = displayName
                }, blueprint.GetDisplayIcon());
            }
            return _entryOrder.Count > 0;
        }

        /// <summary>
        /// 重建"复现制造"列表（数据源：制造历史，UI 层去重）。
        /// 返回是否存在条目。
        /// </summary>
        public bool RebuildForReplication(CraftingSystem craftingSystem)
        {
            ReleaseAll();
            if (craftingSystem == null) return false;

            craftingSystem.GetCraftHistory(_historyCache);
            _dedupeSet.Clear();

            for (int i = 0; i < _historyCache.Count; i++)
            {
                CraftHistoryEntry history = _historyCache[i];
                if (history == null || string.IsNullOrWhiteSpace(history.BlueprintID)) continue;

                // 归一化自定义名用于去重，避免仅因首尾空格不同被视为不同条目
                string normalizedName = string.IsNullOrWhiteSpace(history.CustomName)
                    ? string.Empty
                    : history.CustomName.Trim();

                if (!_dedupeSet.Add(history.BlueprintID + "|" + normalizedName)) continue;

                BlueprintSO blueprint = craftingSystem.GetBlueprint(history.BlueprintID);
                if (blueprint == null) continue;

                string fallback = craftingSystem.GetOriginalProductName(blueprint);
                string displayName = string.IsNullOrWhiteSpace(normalizedName) ? fallback : normalizedName;

                AddEntry(new CraftListEntry
                {
                    EntryKey      = $"R:{i}",
                    BlueprintID   = blueprint.ID,
                    DisplayName   = displayName,
                    SuggestedName = displayName
                }, blueprint.GetDisplayIcon());
            }
            return _entryOrder.Count > 0;
        }

        /// <summary>
        /// 获取当前选中条目的蓝图 ID。
        /// </summary>
        public string GetSelectedBlueprintId()
        {
            if (string.IsNullOrWhiteSpace(SelectedEntryKey)) return string.Empty;
            return _entryByKey.TryGetValue(SelectedEntryKey, out CraftListEntry e) ? e.BlueprintID : string.Empty;
        }

        /// <summary>
        /// 获取当前选中条目的建议默认名称。
        /// </summary>
        public string GetSelectedSuggestedName()
        {
            if (string.IsNullOrWhiteSpace(SelectedEntryKey)) return string.Empty;
            return _entryByKey.TryGetValue(SelectedEntryKey, out CraftListEntry e) ? e.SuggestedName : string.Empty;
        }

        /// <summary>
        /// 移除指定蓝图 ID 对应的所有条目，并返回移除后首个可选条目的 key（无则返回 null）。
        /// </summary>
        public string RemoveByBlueprintId(string blueprintId)
        {
            List<string> toRemove = null;
            for (int i = 0; i < _entryOrder.Count; i++)
            {
                if (!_entryByKey.TryGetValue(_entryOrder[i], out CraftListEntry e)) continue;
                if (!string.Equals(e.BlueprintID, blueprintId, StringComparison.Ordinal)) continue;
                if (toRemove == null) toRemove = new List<string>();
                toRemove.Add(_entryOrder[i]);
            }

            if (toRemove == null) return null;
            for (int i = 0; i < toRemove.Count; i++) RemoveEntry(toRemove[i]);

            return _entryOrder.Count > 0 ? _entryOrder[0] : null;
        }

        /// <summary>
        /// 回收所有列表项并清空索引：
        /// 父类负责通用集合清理，本子类追加清理 _slotByEntryKey 索引。
        /// </summary>
        public override void ReleaseAll()
        {
            base.ReleaseAll();
            _slotByEntryKey.Clear();
        }

        // --- 私有方法 ---

        private void AddEntry(CraftListEntry entry, Sprite icon)
        {
            if (_slotPool == null || _binder.ListRoot == null) return;

            GameObject go = _slotPool.Get();
            go.transform.SetParent(_binder.ListRoot, false);

            BlueprintSlotUI slotUI = go.GetComponent<BlueprintSlotUI>();
            if (slotUI == null)
            {
                DebugTools.LogError("[CraftingListManager] slotPrefab 缺少 BlueprintSlotUI 组件。");
                _slotPool.Release(go);
                return;
            }

            slotUI.Setup(entry.EntryKey, entry.BlueprintID, icon, entry.DisplayName);
            // 父类登记三件套（_activeSlots / _entryByKey / _entryOrder）
            RegisterActiveSlot(entry.EntryKey, entry, slotUI);
            // 本子类额外维护按 key 反查 Slot 的索引
            _slotByEntryKey[entry.EntryKey] = slotUI;
        }

        private void RemoveEntry(string key)
        {
            if (_slotByEntryKey.TryGetValue(key, out BlueprintSlotUI slotUI))
            {
                if (slotUI != null && _slotPool != null)
                    _slotPool.Release(slotUI.gameObject);
                _activeSlots.Remove(slotUI);
            }
            _slotByEntryKey.Remove(key);
            _entryByKey.Remove(key);
            _entryOrder.Remove(key);
        }
    }
}
