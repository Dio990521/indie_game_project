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
        [Header("Data")]
        public List<ItemSO> items = new List<ItemSO>();

        [Header("View")]
        public InventoryUIView inventoryUI;

        public static event Action OnInventoryClosed;

        private void OnEnable()
        {
            BoardActionMenuView.OnRequestOpenInventory += OpenInventory;
            if (inventoryUI != null)
            {
                inventoryUI.OnCloseRequested += CloseInventory;
                inventoryUI.OnSlotClicked += HandleSlotClicked;
            }
        }

        private void OnDisable()
        {
            BoardActionMenuView.OnRequestOpenInventory -= OpenInventory;
            if (inventoryUI != null)
            {
                inventoryUI.OnCloseRequested -= CloseInventory;
                inventoryUI.OnSlotClicked -= HandleSlotClicked;
            }
        }

        public void OpenInventory()
        {
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
    }
}
