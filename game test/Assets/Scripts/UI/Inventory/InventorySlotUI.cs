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
        // 空槽位的本地化占位文本
        [SerializeField] private LocalizedString emptyLabel;

        // 当前槽位绑定的物品
        private ItemSO _item;
        // 点击回调（由外部注入）
        private Action<ItemSO> _onClick;

        /// <summary>
        /// 初始化槽位显示内容与点击逻辑。
        /// </summary>
        /// <param name="item">槽位物品</param>
        /// <param name="onClick">点击回调</param>
        public void Setup(ItemSO item, Action<ItemSO> onClick)
        {
            _item = item;
            _onClick = onClick;
            if (nameLabel == null) return;
            if (item == null || item.ItemName == null)
            {
                if (emptyLabel == null)
                {
                    nameLabel.text = "Empty";
                    return;
                }
                // 异步读取本地化“空”文本
                var emptyHandle = emptyLabel.GetLocalizedStringAsync();
                emptyHandle.Completed += op =>
                {
                    if (_item != item) return;
                    nameLabel.text = op.Result;
                };
                return;
            }

            // 异步读取物品名称（本地化）
            var handle = item.ItemName.GetLocalizedStringAsync();
            handle.Completed += op =>
            {
                if (_item != item) return;
                nameLabel.text = op.Result;
            };
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_item == null) return;
            // 点击后通知外部处理物品逻辑
            _onClick?.Invoke(_item);
        }
    }
}
