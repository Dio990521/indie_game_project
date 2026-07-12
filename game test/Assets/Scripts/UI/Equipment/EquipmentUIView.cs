using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Gameplay.Inventory;
using IndieGame.UI.Inventory;

namespace IndieGame.UI.Equipment
{
    /// <summary>
    /// 装备界面的视图层（View）：
    /// 只负责"如何显示"——把 Controller 已经算好/格式化好的数据塞进 Binder 引用的 UI 控件，
    /// 不订阅 EventBus，不做 Tab 过滤、不做物品/装备状态判断。
    /// 物品格的对象池实例化/销毁需要直接持有 Binder 的 Prefab 引用，因此归属 View（约定：
    /// "决定显示什么"归 Controller，"怎么显示"归 View）。
    /// </summary>
    public class EquipmentUIView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private EquipmentUIBinder binder;

        // ── 物品格对象池 ─────────────────────────────────────────────────
        private readonly List<InventorySlotUI> _slotPool = new List<InventorySlotUI>();
        private int _activeSlotCount;

        // 已生成的属性加成文本行，每次刷新详情前先清空
        private readonly List<TMPro.TMP_Text> _modifierRows = new List<TMPro.TMP_Text>();

        private void Awake()
        {
            WarmupSlotPool();
        }

        // ── 显隐 ─────────────────────────────────────────────────────────

        public void SetVisible(bool visible)
        {
            CanvasGroup canvasGroup = binder?.CanvasGroup;
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        // ── Tab 高亮 ─────────────────────────────────────────────────────

        public void SetTabHighlight(int activeIndex)
        {
            GameObject[] highlights = binder?.CategoryTabHighlights;
            if (highlights == null) return;
            for (int i = 0; i < highlights.Length; i++)
            {
                if (highlights[i] != null) highlights[i].SetActive(i == activeIndex);
            }
        }

        // ── 物品网格 ─────────────────────────────────────────────────────

        private void WarmupSlotPool()
        {
            if (binder?.SlotPrefab == null || binder?.ItemGridRoot == null) return;
            // 装备界面每个 Tab 的物品数量远少于背包，池子不需要预热太多，按需扩容即可。
        }

        private InventorySlotUI GetPooledSlot()
        {
            if (_activeSlotCount < _slotPool.Count)
            {
                InventorySlotUI existing = _slotPool[_activeSlotCount];
                existing.gameObject.SetActive(true);
                _activeSlotCount++;
                return existing;
            }

            InventorySlotUI newSlot = Instantiate(binder.SlotPrefab, binder.ItemGridRoot);
            newSlot.gameObject.SetActive(true);
            _slotPool.Add(newSlot);
            _activeSlotCount++;
            return newSlot;
        }

        private void ReturnExcessToPool()
        {
            for (int i = _activeSlotCount; i < _slotPool.Count; i++)
            {
                _slotPool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 显示物品网格：filteredSlots 是 Controller 已经按当前 Tab 过滤好的槽位列表。
        /// 数量不足 minSlotCount 时用空槽位补齐占位，与背包网格保持一致的视觉规律。
        /// </summary>
        public void DisplayGrid(IReadOnlyList<InventorySlot> filteredSlots, int minSlotCount,
            InventorySlot selectedSlot, Action<InventorySlot> onClick)
        {
            if (binder?.SlotPrefab == null || binder?.ItemGridRoot == null) return;

            _activeSlotCount = 0;
            int filledCount = 0;

            if (filteredSlots != null)
            {
                for (int i = 0; i < filteredSlots.Count; i++)
                {
                    InventorySlotUI slotUI = GetPooledSlot();
                    slotUI.Setup(filteredSlots[i], onClick);
                    filledCount++;
                }
            }

            for (int i = filledCount; i < minSlotCount; i++)
            {
                InventorySlotUI emptySlotUI = GetPooledSlot();
                emptySlotUI.Setup(null, null);
            }

            ReturnExcessToPool();
            SetSelectionHighlight(selectedSlot);
        }

        /// <summary>
        /// 仅刷新选中高亮，不重建格子内容（用于点击选中时的轻量刷新）。
        /// </summary>
        public void SetSelectionHighlight(InventorySlot selectedSlot)
        {
            for (int i = 0; i < _activeSlotCount; i++)
            {
                InventorySlotUI slotUI = _slotPool[i];
                slotUI.SetSelected(selectedSlot != null && slotUI.BoundSlot == selectedSlot);
            }
        }

        public void ScrollGridToTop()
        {
            if (binder?.GridScrollRect != null) binder.GridScrollRect.verticalNormalizedPosition = 1f;
        }

        // ── 详情面板 ─────────────────────────────────────────────────────

        public void SetDetail(Sprite icon, string name, string typeLabel, Color rarityColor, string rarityLabel,
            string description, IReadOnlyList<string> modifierLines)
        {
            if (binder == null) return;

            if (binder.DetailIcon != null) binder.DetailIcon.sprite = icon;
            if (binder.DetailNameText != null) binder.DetailNameText.text = name ?? string.Empty;
            if (binder.DetailTypeText != null) binder.DetailTypeText.text = typeLabel ?? string.Empty;
            if (binder.DetailDescText != null) binder.DetailDescText.text = description ?? string.Empty;
            if (binder.DetailRarityBadgeBackground != null) binder.DetailRarityBadgeBackground.color = rarityColor;
            if (binder.DetailRarityBadgeText != null) binder.DetailRarityBadgeText.text = rarityLabel ?? string.Empty;

            RefreshModifierRows(modifierLines);
        }

        public void ClearDetail()
        {
            if (binder == null) return;

            if (binder.DetailIcon != null) binder.DetailIcon.sprite = null;
            if (binder.DetailNameText != null) binder.DetailNameText.text = string.Empty;
            if (binder.DetailTypeText != null) binder.DetailTypeText.text = string.Empty;
            if (binder.DetailDescText != null) binder.DetailDescText.text = string.Empty;
            if (binder.DetailRarityBadgeText != null) binder.DetailRarityBadgeText.text = string.Empty;

            RefreshModifierRows(null);
        }

        // 属性加成条目数量少（通常 0~4 条）且只在选中变化时刷新一次，不是 Update() 高频路径，
        // 因此用简单的“清空重建”即可，没必要为此单独维护一套对象池。
        private void RefreshModifierRows(IReadOnlyList<string> modifierLines)
        {
            for (int i = 0; i < _modifierRows.Count; i++)
            {
                if (_modifierRows[i] != null) Destroy(_modifierRows[i].gameObject);
            }
            _modifierRows.Clear();

            if (modifierLines == null || binder?.ModifierRowPrefab == null || binder?.DetailModifiersRoot == null)
                return;

            for (int i = 0; i < modifierLines.Count; i++)
            {
                TMPro.TMP_Text row = Instantiate(binder.ModifierRowPrefab, binder.DetailModifiersRoot);
                row.text = modifierLines[i];
                row.gameObject.SetActive(true);
                _modifierRows.Add(row);
            }
        }

        public void SetEquipButton(string label, bool interactable)
        {
            if (binder?.EquipButtonLabel != null) binder.EquipButtonLabel.text = label ?? string.Empty;
            if (binder?.EquipButton != null) binder.EquipButton.interactable = interactable;
        }

        // ── 已装备槽位 ───────────────────────────────────────────────────

        public void SetEquippedWeapon(ItemSO item, Action onClick) => binder?.EquippedWeaponSlot?.Refresh(item, onClick);

        public void SetEquippedArmor(ItemSO item, Action onClick) => binder?.EquippedArmorSlot?.Refresh(item, onClick);

        /// <summary>
        /// 配方部位尚未实现任何数据/规则，两个配方槽恒为空槽占位，不接收点击回调。
        /// </summary>
        public void ClearEquippedRecipeSlots()
        {
            binder?.EquippedRecipeSlot0?.Refresh(null, null);
            binder?.EquippedRecipeSlot1?.Refresh(null, null);
        }
    }
}
