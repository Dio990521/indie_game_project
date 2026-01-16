using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Inventory
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI")]
        public GameObject rootPanel;
        public Transform contentRoot;
        public InventorySlotUI slotPrefab;
        public Button closeButton;

        public event Action OnCloseRequested;
        public event Action<ItemSO> OnSlotClicked;

        private readonly List<InventorySlotUI> _slots = new List<InventorySlotUI>();

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        public void Show(List<ItemSO> items)
        {
            if (rootPanel != null) rootPanel.SetActive(true);
            Rebuild(items);
        }

        public void Hide()
        {
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        private void HandleCloseClicked()
        {
            OnCloseRequested?.Invoke();
        }

        private void Rebuild(List<ItemSO> items)
        {
            ClearSlots();
            if (items == null || slotPrefab == null || contentRoot == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                InventorySlotUI slot = Instantiate(slotPrefab, contentRoot);
                slot.Setup(items[i], HandleSlotClicked);
                _slots.Add(slot);
            }
        }

        private void HandleSlotClicked(ItemSO item)
        {
            OnSlotClicked?.Invoke(item);
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null) Destroy(_slots[i].gameObject);
            }
            _slots.Clear();
        }
    }
}
