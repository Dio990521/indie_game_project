using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 打造界面右侧详情面板管理器（纯 C# 辅助类）：
    /// 职责：需求槽位对象池、需求列表刷新、制造按钮状态、空态/详情面板显隐。
    /// 由 CraftingUIController 在 Awake 中实例化并持有。
    /// </summary>
    internal class CraftingDetailPanel
    {
        private GameObjectPool _requirementPool;
        private CraftUIBinder _binder;

        private readonly List<RequirementSlotUI> _activeRequirementSlots = new List<RequirementSlotUI>();
        // 背包数量缓存：避免每次刷新都遍历 slots，在 RefreshRequirementList 前统一重建一次
        private readonly Dictionary<ItemSO, int> _inventoryCountCache = new Dictionary<ItemSO, int>();

        /// <summary>
        /// 初始化：创建需求槽位对象池，必须在 Awake 中调用。
        /// </summary>
        public void Init(CraftUIBinder binder, int requirementPoolWarmup)
        {
            _binder = binder;
            if (binder.RequirementSlotPrefab != null && binder.RequirementsRoot != null)
                _requirementPool = new GameObjectPool(binder.RequirementSlotPrefab, binder.RequirementsRoot, requirementPoolWarmup);
        }

        /// <summary>
        /// 选中条目后进入详情态：隐藏空态节点、显示面板、更新成品图标。
        /// 调用后需紧接调用 RefreshRequirementList 和 RefreshButtonState。
        /// </summary>
        public void ShowForEntry(BlueprintSO blueprint)
        {
            if (_binder.EmptyStateNode != null)
                _binder.EmptyStateNode.SetActive(false);

            SetDetailPanelVisible(true);

            if (_binder.ProductIcon != null)
            {
                _binder.ProductIcon.sprite = blueprint.GetDisplayIcon();
                _binder.ProductIcon.enabled = _binder.ProductIcon.sprite != null;
            }
        }

        /// <summary>
        /// 刷新右侧材料列表（两个 Tab 通用）。
        /// </summary>
        public void RefreshRequirementList(string blueprintId)
        {
            ReleaseAllRequirementSlots();
            if (string.IsNullOrWhiteSpace(blueprintId)) return;

            CraftingSystem cs = CraftingSystem.Instance;
            BlueprintSO blueprint = cs != null ? cs.GetBlueprint(blueprintId) : null;
            if (blueprint == null || _binder.RequirementsRoot == null) return;

            RebuildInventoryCountCache();

            IReadOnlyList<BlueprintRequirement> reqs = blueprint.Requirements;
            for (int i = 0; i < reqs.Count; i++)
            {
                RequirementSlotUI ui = SpawnRequirementSlot();
                if (ui == null) continue;

                int owned = 0;
                if (reqs[i] != null && reqs[i].Item != null)
                    _inventoryCountCache.TryGetValue(reqs[i].Item, out owned);

                ui.Setup(reqs[i], owned);
                _activeRequirementSlots.Add(ui);
            }
        }

        /// <summary>
        /// 刷新制造按钮可点击状态（两个 Tab 通用）。
        /// </summary>
        public void RefreshButtonState(string blueprintId)
        {
            if (_binder.CraftButton == null) return;

            if (string.IsNullOrWhiteSpace(blueprintId))
            {
                _binder.CraftButton.interactable = false;
                return;
            }

            CraftingSystem cs = CraftingSystem.Instance;
            _binder.CraftButton.interactable = cs != null && cs.CanCraft(blueprintId);
        }

        /// <summary>
        /// 进入空态：显示空态节点、隐藏详情面板、清空材料列表、禁用制造按钮。
        /// </summary>
        public void EnterEmptyState()
        {
            if (_binder.EmptyStateNode != null)
                _binder.EmptyStateNode.SetActive(true);

            SetDetailPanelVisible(false);

            if (_binder.ProductIcon != null)
            {
                _binder.ProductIcon.sprite = null;
                _binder.ProductIcon.enabled = false;
            }

            ReleaseAllRequirementSlots();

            if (_binder.CraftButton != null)
                _binder.CraftButton.interactable = false;
        }

        /// <summary>
        /// 回收所有需求槽位（不影响详情面板其他元素）。
        /// </summary>
        public void ReleaseAllRequirementSlots()
        {
            for (int i = 0; i < _activeRequirementSlots.Count; i++)
            {
                if (_activeRequirementSlots[i] != null && _requirementPool != null)
                    _requirementPool.Release(_activeRequirementSlots[i].gameObject);
            }
            _activeRequirementSlots.Clear();
        }

        // --- 私有方法 ---

        private void SetDetailPanelVisible(bool visible)
        {
            if (_binder.ProductIcon != null)
                _binder.ProductIcon.gameObject.SetActive(visible);
            if (_binder.RequirementsRoot != null)
                _binder.RequirementsRoot.gameObject.SetActive(visible);
            if (_binder.CraftButton != null)
                _binder.CraftButton.gameObject.SetActive(visible);
        }

        private RequirementSlotUI SpawnRequirementSlot()
        {
            if (_requirementPool == null || _binder.RequirementsRoot == null) return null;

            GameObject go = _requirementPool.Get();
            go.transform.SetParent(_binder.RequirementsRoot, false);

            RequirementSlotUI ui = go.GetComponent<RequirementSlotUI>();
            if (ui == null)
            {
                DebugTools.LogError("[CraftingDetailPanel] requirementSlotPrefab 缺少 RequirementSlotUI 组件。");
                _requirementPool.Release(go);
                return null;
            }
            return ui;
        }

        private void RebuildInventoryCountCache()
        {
            _inventoryCountCache.Clear();
            InventoryManager inv = InventoryManager.Instance;
            if (inv == null || inv.slots == null) return;

            for (int i = 0; i < inv.slots.Count; i++)
            {
                InventorySlot slot = inv.slots[i];
                if (slot == null || slot.Item == null || slot.Count <= 0) continue;

                if (_inventoryCountCache.TryGetValue(slot.Item, out int cur))
                    _inventoryCountCache[slot.Item] = cur + slot.Count;
                else
                    _inventoryCountCache[slot.Item] = slot.Count;
            }
        }
    }
}
