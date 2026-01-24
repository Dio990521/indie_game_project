using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.UI;
using IndieGame.Core;

namespace IndieGame.Gameplay.Inventory
{
    public class InventoryManager : MonoSingleton<InventoryManager>
    {
        protected override bool DestroyOnLoad => true;
        [Header("Data")]
        public List<ItemSO> items = new List<ItemSO>();

        public static event Action<List<ItemSO>> OnInventoryUpdated;
        public static event Action OnInventoryOpened;
        public static event Action OnInventoryClosed;

        private void OnEnable()
        {
            EventBus.Subscribe<OpenInventoryEvent>(HandleOpenInventory);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OpenInventoryEvent>(HandleOpenInventory);
        }

        private void HandleOpenInventory(OpenInventoryEvent evt)
        {
            OpenInventory();
        }

        public void OpenInventory()
        {
            OnInventoryUpdated?.Invoke(items);
            OnInventoryOpened?.Invoke();
        }

        public void CloseInventory()
        {
            OnInventoryClosed?.Invoke();
        }

        public void UseItem(ItemSO item)
        {
            if (item == null) return;
            item.Use();
        }
    }
}
