using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Equipment;
using IndieGame.Gameplay.Inventory;
using IndieGame.UI.Common;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 强化界面左侧"武器列表"管理器：数据源 = 装备中的武器（若有）+ 背包里的武器槽位。
    /// </summary>
    internal class WeaponEnhanceListManager : BaseListManager<WeaponEnhanceListManager.WeaponEntry, WeaponEnhanceSlotUI>
    {
        internal struct WeaponEntry
        {
            public InventorySlot Slot;
            public bool IsEquipped;
        }

        private CraftUIBinder _binder;

        public void Init(CraftUIBinder binder, int slotPoolWarmup)
        {
            _binder = binder;
            if (binder.WeaponSlotPrefab != null && binder.WeaponListRoot != null)
                _slotPool = new GameObjectPool(binder.WeaponSlotPrefab, binder.WeaponListRoot, slotPoolWarmup);
        }

        /// <summary>
        /// 重建武器列表。返回是否存在条目。
        /// </summary>
        public bool Rebuild(GameObject player)
        {
            ReleaseAll();

            int index = 0;

            WeaponEquipController equip = player != null ? player.GetComponent<WeaponEquipController>() : null;
            if (equip != null && equip.CurrentWeaponSlot != null)
            {
                AddEntry($"E:{index++}", new WeaponEntry { Slot = equip.CurrentWeaponSlot, IsEquipped = true });
            }

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null && inventory.slots != null)
            {
                for (int i = 0; i < inventory.slots.Count; i++)
                {
                    InventorySlot slot = inventory.slots[i];
                    if (slot == null || !(slot.Item is WeaponSO)) continue;
                    AddEntry($"B:{index++}", new WeaponEntry { Slot = slot, IsEquipped = false });
                }
            }

            return _entryOrder.Count > 0;
        }

        public InventorySlot GetSelectedSlot()
        {
            if (string.IsNullOrWhiteSpace(SelectedEntryKey)) return null;
            return _entryByKey.TryGetValue(SelectedEntryKey, out WeaponEntry e) ? e.Slot : null;
        }

        public bool IsSelectedEquipped()
        {
            if (string.IsNullOrWhiteSpace(SelectedEntryKey)) return false;
            return _entryByKey.TryGetValue(SelectedEntryKey, out WeaponEntry e) && e.IsEquipped;
        }

        private void AddEntry(string entryKey, WeaponEntry entry)
        {
            if (_slotPool == null || _binder.WeaponListRoot == null) return;

            GameObject go = _slotPool.Get();
            go.transform.SetParent(_binder.WeaponListRoot, false);

            WeaponEnhanceSlotUI slotUI = go.GetComponent<WeaponEnhanceSlotUI>();
            if (slotUI == null)
            {
                DebugTools.LogError("[WeaponEnhanceListManager] weaponSlotPrefab 缺少 WeaponEnhanceSlotUI 组件。");
                _slotPool.Release(go);
                return;
            }

            WeaponSO weapon = entry.Slot?.Item as WeaponSO;
            string displayName = entry.Slot != null && !string.IsNullOrWhiteSpace(entry.Slot.CustomName)
                ? entry.Slot.CustomName
                : (weapon != null ? weapon.GetLocalizedName() : "Unknown");

            slotUI.Setup(entryKey, weapon != null ? weapon.Icon : null, displayName, entry.IsEquipped);
            RegisterActiveSlot(entryKey, entry, slotUI);
        }
    }
}
