using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using IndieGame.Gameplay.Inventory;
using UnityEngine.Localization;

namespace IndieGame.UI.Inventory
{
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        public TMP_Text nameLabel;
        [SerializeField] private LocalizedString emptyLabel;

        private ItemSO _item;
        private Action<ItemSO> _onClick;

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
                var emptyHandle = emptyLabel.GetLocalizedStringAsync();
                emptyHandle.Completed += op =>
                {
                    if (_item != item) return;
                    nameLabel.text = op.Result;
                };
                return;
            }

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
            _onClick?.Invoke(_item);
        }
    }
}
