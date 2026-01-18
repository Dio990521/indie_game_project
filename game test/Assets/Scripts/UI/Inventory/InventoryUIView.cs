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

        public event Action OnCloseRequested;
        public event Action<ItemSO> OnSlotClicked;

        private readonly List<InventorySlotUI> _slots = new List<InventorySlotUI>();
        private CanvasGroup _canvasGroup;
        private bool _useCanvasGroup = false;

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
            SetupVisibility();
            SetVisible(false);
        }

        private void Start()
        {
            if (binder != null)
            {
                Transform root = binder.RootPanel != null ? binder.RootPanel.transform : transform;
                Canvas canvas = root.GetComponentInParent<Canvas>();
            }
        }

        private void OnEnable()
        {
            InventoryManager.OnInventoryUpdated += HandleRefresh;
            InventoryManager.OnInventoryOpened += HandleOpen;
            InventoryManager.OnInventoryClosed += HandleClose;
        }

        private void OnDisable()
        {
            InventoryManager.OnInventoryUpdated -= HandleRefresh;
            InventoryManager.OnInventoryOpened -= HandleOpen;
            InventoryManager.OnInventoryClosed -= HandleClose;
        }

        public void Show(List<ItemSO> items)
        {
            SetVisible(true);
            Rebuild(items);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void HandleCloseClicked()
        {
            OnCloseRequested?.Invoke();
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null) inv.CloseInventory();
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
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null) inv.UseItem(item);
        }

        private void HandleRefresh(List<ItemSO> items)
        {
            Rebuild(items);
        }

        private void HandleOpen()
        {
            SetVisible(true);
        }

        private void HandleClose()
        {
            SetVisible(false);
        }

        private void SetupVisibility()
        {
            if (binder.RootPanel == null) return;
            if (binder.RootPanel == gameObject)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                _useCanvasGroup = true;
            }
        }

        private void SetVisible(bool visible)
        {
            if (binder.RootPanel == null) return;
            if (_useCanvasGroup && _canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
                return;
            }
            binder.RootPanel.SetActive(visible);
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
