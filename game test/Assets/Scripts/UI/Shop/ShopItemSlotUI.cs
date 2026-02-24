using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IndieGame.Core;

namespace IndieGame.UI.Shop
{
    /// <summary>
    /// 商店商品列表项 UI：
    /// 负责把“图标 / 名称 / 单价”渲染到单条 Slot。
    ///
    /// 交互约束：
    /// - 点击整个 Slot 即选中；
    /// - 不使用 Action 回调；
    /// - 统一通过 EventBus 广播 ShopItemSlotClickedEvent。
    /// </summary>
    public class ShopItemSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;

        // 运行时绑定数据（用于点击事件回传）
        private string _entryKey;
        private string _shopId;
        private string _shopEntryId;

        /// <summary>
        /// 设置列表项显示：
        /// 左到右顺序：图标、名称、单价。
        /// </summary>
        public void Setup(string entryKey, string shopId, string shopEntryId, Sprite icon, string displayName, int unitPrice)
        {
            _entryKey = string.IsNullOrWhiteSpace(entryKey) ? string.Empty : entryKey;
            _shopId = string.IsNullOrWhiteSpace(shopId) ? string.Empty : shopId;
            _shopEntryId = string.IsNullOrWhiteSpace(shopEntryId) ? string.Empty : shopEntryId;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Unknown Item" : displayName;
            }

            if (priceText != null)
            {
                priceText.text = Mathf.Max(0, unitPrice) + " G";
            }
        }

        /// <summary>
        /// 点击整个 Slot：
        /// 广播条目选择事件给 ShopUIController。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_entryKey)) return;
            EventBus.Raise(new ShopItemSlotClickedEvent
            {
                EntryKey = _entryKey,
                ShopID = _shopId,
                ShopEntryID = _shopEntryId
            });
        }
    }
}
