using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Inventory
{
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        public TMP_Text nameLabel;

        private ItemSO _item;
        private Action<ItemSO> _onClick;

        public void Setup(ItemSO item, Action<ItemSO> onClick)
        {
            _item = item;
            _onClick = onClick;
            if (nameLabel != null) nameLabel.text = item != null ? item.ItemName : "Empty";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_item == null) return;
            _onClick?.Invoke(_item);
        }
    }
}
