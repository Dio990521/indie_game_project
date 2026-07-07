using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using IndieGame.Gameplay.Inventory;
using IndieGame.Core.Utilities;
using IndieGame.UI.Common;
using UnityEngine.Localization;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包槽位 UI：
    /// 负责显示物品图标和数量，并在点击时触发回调。
    /// nameLabel 保留兼容旧版 UI，新全屏背包界面不使用（物品名改由详情面板展示）。
    /// </summary>
    public class InventorySlotUI : BaseSlotUI
    {
        // 物品图标（新全屏背包界面使用）
        [SerializeField] private Image iconImage;
        // 物品名称文本（旧版 UI 兼容，新界面可留空）
        public TMP_Text nameLabel;
        // 数量文本（仅当数量 > 1 时显示）
        [SerializeField] private TMP_Text countLabel;
        // 稀有度背景色块：颜色随物品稀有度变化，空槛位时隐藏
        [SerializeField] private Image rarityBackground;
        // 选中高亮：简单占位实现，默认隐藏，由 Controller 在选中/取消选中时调用 SetSelected 控制
        [SerializeField] private GameObject selectedHighlight;
        // Inspector 配置指南：
        // - 将 countLabel 绑定到槽位预制体上的 TextMeshProUGUI
        // - 当数量 <= 1 时将自动清空文本
        // 空槽位的本地化占位文本
        [SerializeField] private LocalizedString emptyLabel;

        // 当前槽位绑定的物品
        private InventorySlot _slot;
        // 点击回调（由外部注入）
        private Action<InventorySlot> _onClick;

        // 供 Controller 反查"这个槛位 UI 当前绑定的是哪个 slot"，用于刷新选中高亮
        public InventorySlot BoundSlot => _slot;

        /// <summary>
        /// 初始化槽位显示内容与点击逻辑。
        /// </summary>
        /// <param name="slot">槽位数据</param>
        /// <param name="onClick">点击回调</param>
        public void Setup(InventorySlot slot, Action<InventorySlot> onClick)
        {
            _slot = slot;
            _onClick = onClick;

            // 设置图标（null 安全，旧版预制体可不绑定 iconImage）
            if (iconImage != null)
                iconImage.sprite = slot?.Item?.Icon;

            // 稀有度背景色块：空槛位/空物品时隐藏，否则按 ItemSO.Rarity 上色
            if (rarityBackground != null)
            {
                bool hasItem = slot?.Item != null;
                rarityBackground.enabled = hasItem;
                if (hasItem) rarityBackground.color = ItemRarityUtility.GetColor(slot.Item.Rarity);
            }

            // 复用时先清空选中高亮，真正的选中态由 Controller 在 Rebuild 之后统一刷新
            SetSelected(false);

            if (nameLabel == null) return;
            if (slot == null || slot.Item == null)
            {
                if (emptyLabel == null)
                {
                    nameLabel.text = "Empty";
                    if (countLabel != null) countLabel.text = string.Empty;
                    return;
                }
                // 异步读取本地化“空”文本：
                // 1) _slot != slot：异步期间槽位已被重新绑定到其他物品，丢弃旧结果；
                // 2) this == null / nameLabel == null：GameObject 已被销毁或 nameLabel 被解除引用，
                //    Unity 的 == 重载会把已销毁的 UnityEngine.Object 判定为 null，避免 NRE。
                var emptyHandle = emptyLabel.GetLocalizedStringAsync();
                emptyHandle.Completed += op =>
                {
                    if (this == null || nameLabel == null) return;
                    if (_slot != slot) return;
                    nameLabel.text = op.Result;
                    if (countLabel != null) countLabel.text = string.Empty;
                };
                return;
            }

            // 优先显示槽位实例名（用于支持制造时的自定义命名）
            if (!string.IsNullOrWhiteSpace(slot.CustomName))
            {
                nameLabel.text = slot.CustomName;
            }
            // 若没有实例名，则回退到 ItemSO 原始名称
            else if (slot.Item.ItemName != null)
            {
                // 异步读取物品名称（本地化）
                // 防御性校验同上：先确认对象与 UI 引用仍存活，再校验槽位绑定未漂移。
                var handle = slot.Item.ItemName.GetLocalizedStringAsync();
                handle.Completed += op =>
                {
                    if (this == null || nameLabel == null) return;
                    if (_slot != slot) return;
                    nameLabel.text = op.Result;
                };
            }
            else
            {
                nameLabel.text = string.IsNullOrWhiteSpace(slot.Item.ID) ? "Unknown Item" : slot.Item.ID;
            }

            // 数量显示：始终显示，与设计图保持一致（非堆叠物品也显示 1）
            if (countLabel != null)
                countLabel.text = slot.Count > 0 ? slot.Count.ToString() : string.Empty;
        }

        /// <summary>
        /// 选中高亮：简单占位实现（GameObject 开关），由 Controller 统一调度。
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null) selectedHighlight.SetActive(selected);
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            if (_slot == null || _slot.Item == null) return;
            // 点击后通知外部处理物品逻辑
            _onClick?.Invoke(_slot);
        }
    }
}
