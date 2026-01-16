using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Inventory
{
    public class InventoryUIView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private InventoryUIBinder binder;
        [SerializeField] private UILayerPriority layer = UILayerPriority.Top75;

        public event Action OnCloseRequested;
        public event Action<ItemSO> OnSlotClicked;

        private readonly List<InventorySlotUI> _slots = new List<InventorySlotUI>();

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[InventoryUIView] Missing binder reference.");
                return;
            }
            if (binder.CloseButton != null)
            {
                binder.CloseButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void Start()
        {
            if (binder != null)
            {
                Transform root = binder.RootPanel != null ? binder.RootPanel.transform : transform;
                UIManager.Instance.AttachToLayer(root, layer);
            }
        }

        public void Show(List<ItemSO> items)
        {
            if (binder.RootPanel != null) binder.RootPanel.SetActive(true);
            Rebuild(items);
        }

        public void Hide()
        {
            if (binder.RootPanel != null) binder.RootPanel.SetActive(false);
        }

        private void HandleCloseClicked()
        {
            OnCloseRequested?.Invoke();
        }

        private void Rebuild(List<ItemSO> items)
        {
            ClearSlots();
            if (items == null || binder.SlotPrefab == null || binder.ContentRoot == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                InventorySlotUI slot = Instantiate(binder.SlotPrefab, binder.ContentRoot);
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
