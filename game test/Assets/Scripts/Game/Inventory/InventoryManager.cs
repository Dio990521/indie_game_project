using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.UI;
using IndieGame.UI.Inventory;

namespace IndieGame.Gameplay.Inventory
{
    public class InventoryManager : MonoSingleton<InventoryManager>
    {
        protected override bool DestroyOnLoad => true;
        [Header("Data")]
        public List<ItemSO> items = new List<ItemSO>();

        [Header("View")]
        public InventoryUIView inventoryUI;
        private bool _uiBound = false;

        public static event Action OnInventoryClosed;

        private void OnEnable()
        {
            BoardActionMenuView.OnRequestOpenInventory += OpenInventory;
            IndieGame.UI.UIManager.OnUIReady += HandleUIReady;
            TryBindUI();
        }

        private void OnDisable()
        {
            BoardActionMenuView.OnRequestOpenInventory -= OpenInventory;
            IndieGame.UI.UIManager.OnUIReady -= HandleUIReady;
            UnbindUI();
        }

        public void OpenInventory()
        {
            TryBindUI();
            if (inventoryUI == null) return;
            inventoryUI.Show(items);
        }

        public void CloseInventory()
        {
            if (inventoryUI == null) return;
            inventoryUI.Hide();
            OnInventoryClosed?.Invoke();
        }

        private void HandleSlotClicked(ItemSO item)
        {
            if (item == null) return;
            item.Use();
        }

        private void TryBindUI()
        {
            if ((inventoryUI == null || inventoryUI.Equals(null)) && IndieGame.UI.UIManager.Instance != null)
            {
                inventoryUI = IndieGame.UI.UIManager.Instance.InventoryInstance;
            }

            if (inventoryUI == null || _uiBound) return;
            inventoryUI.OnCloseRequested += CloseInventory;
            inventoryUI.OnSlotClicked += HandleSlotClicked;
            _uiBound = true;
        }

        private void HandleUIReady()
        {
            TryBindUI();
        }

        private void UnbindUI()
        {
            if (inventoryUI == null || !_uiBound) return;
            inventoryUI.OnCloseRequested -= CloseInventory;
            inventoryUI.OnSlotClicked -= HandleSlotClicked;
            _uiBound = false;
        }
    }
}
