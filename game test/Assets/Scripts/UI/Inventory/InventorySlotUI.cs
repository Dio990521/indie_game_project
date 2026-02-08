using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using IndieGame.Gameplay.Inventory;
using UnityEngine.Localization;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包槽位 UI：
    /// 负责显示物品名称，并在点击时触发回调。
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        // 物品名称文本
        public TMP_Text nameLabel;
        // 数量文本（仅当数量 > 1 时显示）
        [SerializeField] private TMP_Text countLabel;
        // Inspector 配置指南：
        // - 将 countLabel 绑定到槽位预制体上的 TextMeshProUGUI
        // - 当数量 <= 1 时将自动清空文本
        // 空槽位的本地化占位文本
        [SerializeField] private LocalizedString emptyLabel;

        // 当前槽位绑定的物品
        private InventorySlot _slot;
        // 点击回调（由外部注入）
        private Action<InventorySlot> _onClick;

        /// <summary>
        /// 初始化槽位显示内容与点击逻辑。
        /// </summary>
        /// <param name="slot">槽位数据</param>
        /// <param name="onClick">点击回调</param>
        public void Setup(InventorySlot slot, Action<InventorySlot> onClick)
        {
            _slot = slot;
            _onClick = onClick;
            if (nameLabel == null) return;
            if (slot == null || slot.Item == null)
            {
                if (emptyLabel == null)
                {
                    nameLabel.text = "Empty";
                    if (countLabel != null) countLabel.text = string.Empty;
                    return;
                }
                // 异步读取本地化“空”文本
                var emptyHandle = emptyLabel.GetLocalizedStringAsync();
                emptyHandle.Completed += op =>
                {
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
                var handle = slot.Item.ItemName.GetLocalizedStringAsync();
                handle.Completed += op =>
                {
                    if (_slot != slot) return;
                    nameLabel.text = op.Result;
                };
            }
            else
            {
                nameLabel.text = string.IsNullOrWhiteSpace(slot.Item.ID) ? "Unknown Item" : slot.Item.ID;
            }

            // 数量显示：当数量 > 1 时显示数字，否则清空
            if (countLabel != null)
            {
                countLabel.text = slot.Count > 1 ? slot.Count.ToString() : string.Empty;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_slot == null || _slot.Item == null) return;
            // 点击后通知外部处理物品逻辑
            _onClick?.Invoke(_slot);
        }
    }
}
