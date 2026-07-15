using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Core;
using IndieGame.Gameplay.Equipment;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// InventoryManager 的存档 partial：
    /// 包含 ISaveable 实现（CaptureState/RestoreState）、装备延迟恢复协程、
    /// ItemDatabase 索引与存档数据结构。
    /// 背包核心操作（增删/堆叠/排序/开关界面）见主文件 InventoryManager.cs。
    /// </summary>
    public partial class InventoryManager
    {
        /// <summary>
        /// SaveManager 调用：捕获背包 + 已装备武器/防具的完整物品状态。
        /// 装备中的槽位不在 slots 列表里（装备时被 RemoveSlot 摘出），
        /// 必须单独采集，否则读档后装备会凭空消失。
        /// </summary>
        public override object CaptureState()
        {
            InventorySaveState state = new InventorySaveState();

            // 1) 背包槽位
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotSaveData data = BuildSlotSaveData(slots[i], LocationBag);
                if (data != null) state.Slots.Add(data);
            }

            // 2) 已装备的武器/防具（从玩家对象上的控制器采集）
            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            if (player != null)
            {
                WeaponEquipController weaponCtrl = player.GetComponent<WeaponEquipController>();
                InventorySlotSaveData weaponData = BuildSlotSaveData(weaponCtrl != null ? weaponCtrl.CurrentWeaponSlot : null, LocationWeapon);
                if (weaponData != null) state.Slots.Add(weaponData);

                ArmorEquipController armorCtrl = player.GetComponent<ArmorEquipController>();
                InventorySlotSaveData armorData = BuildSlotSaveData(armorCtrl != null ? armorCtrl.CurrentArmorSlot : null, LocationArmor);
                if (armorData != null) state.Slots.Add(armorData);
            }

            return state;
        }

        /// <summary>
        /// SaveManager 调用：恢复背包与装备状态。
        /// 玩家对象可能晚于读档创建（标题界面读档 → 玩法场景才生成玩家），
        /// 因此装备部分先缓存为 pending，由协程轮询等玩家就绪后再应用。
        /// </summary>
        public override void RestoreState(object data)
        {
            if (!(data is InventorySaveState state) || state.Slots == null) return;

            if (!EnsureItemIndex())
            {
                DebugTools.LogError("[InventoryManager] 未配置 ItemDatabaseSO，无法从存档恢复背包。请在 Inspector 中指定物品数据库。");
                return;
            }

            slots.Clear();
            _pendingWeaponRestore = null;
            _pendingArmorRestore = null;

            for (int i = 0; i < state.Slots.Count; i++)
            {
                InventorySlotSaveData saved = state.Slots[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.ItemID)) continue;

                switch (saved.Location)
                {
                    case LocationWeapon:
                        _pendingWeaponRestore = saved;
                        break;
                    case LocationArmor:
                        _pendingArmorRestore = saved;
                        break;
                    default:
                        InventorySlot slot = BuildSlotFromSaveData(saved);
                        if (slot != null) slots.Add(slot);
                        break;
                }
            }

            NotifyInventoryChanged();

            // 装备恢复统一走 pending 流程：
            // 即使存档里没有装备条目，也要执行一次"清空当前装备"，
            // 避免"本局装备了武器 → 读了一份没装备的旧档"后武器残留。
            _hasPendingEquipmentRestore = true;
            TryApplyPendingEquipmentRestore();

            if (_hasPendingEquipmentRestore && isActiveAndEnabled)
            {
                // 玩家未就绪：启动轮询协程等待玩家创建后补应用
                if (_equipmentRestoreRoutine != null) StopCoroutine(_equipmentRestoreRoutine);
                _equipmentRestoreRoutine = StartCoroutine(WaitAndApplyEquipmentRestore());
            }
        }

        /// <summary>
        /// 轮询等待玩家对象就绪后应用装备恢复（帧数上限防止无玩家场景下无限等待）。
        /// </summary>
        private IEnumerator WaitAndApplyEquipmentRestore()
        {
            const int maxWaitFrames = 1800; // 约 30 秒（60fps），超时放弃并保留 pending 供下次读档覆盖
            for (int i = 0; i < maxWaitFrames && _hasPendingEquipmentRestore; i++)
            {
                TryApplyPendingEquipmentRestore();
                if (!_hasPendingEquipmentRestore) yield break;
                yield return null;
            }
            _equipmentRestoreRoutine = null;
        }

        /// <summary>
        /// 尝试把 pending 装备数据应用到玩家的装备控制器上。
        /// </summary>
        private void TryApplyPendingEquipmentRestore()
        {
            if (!_hasPendingEquipmentRestore) return;

            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            if (player == null) return;

            WeaponEquipController weaponCtrl = player.GetComponent<WeaponEquipController>();
            if (weaponCtrl != null)
            {
                weaponCtrl.RestoreEquipped(BuildSlotFromSaveData(_pendingWeaponRestore));
            }

            ArmorEquipController armorCtrl = player.GetComponent<ArmorEquipController>();
            if (armorCtrl != null)
            {
                armorCtrl.RestoreEquipped(BuildSlotFromSaveData(_pendingArmorRestore));
            }

            _pendingWeaponRestore = null;
            _pendingArmorRestore = null;
            _hasPendingEquipmentRestore = false;
        }

        /// <summary>
        /// 槽位 → 存档数据（null 槽位返回 null，调用方自行跳过）。
        /// </summary>
        private static InventorySlotSaveData BuildSlotSaveData(InventorySlot slot, int location)
        {
            if (slot == null || slot.Item == null || slot.Count <= 0) return null;
            if (string.IsNullOrWhiteSpace(slot.Item.ID))
            {
                DebugTools.LogWarning($"[InventoryManager] 物品 {slot.Item.name} 缺少 ID，无法写入存档，已跳过。");
                return null;
            }

            return new InventorySlotSaveData
            {
                ItemID = slot.Item.ID,
                Count = slot.Count,
                CustomName = slot.CustomName ?? string.Empty,
                Location = location
            };
        }

        /// <summary>
        /// 存档数据 → 槽位（数据库查不到 ID 时返回 null 并打警告）。
        /// </summary>
        private InventorySlot BuildSlotFromSaveData(InventorySlotSaveData saved)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.ItemID)) return null;
            if (!EnsureItemIndex()) return null;

            if (!_itemById.TryGetValue(saved.ItemID, out ItemSO item) || item == null)
            {
                DebugTools.LogWarning($"[InventoryManager] 存档物品 ID \"{saved.ItemID}\" 在 ItemDatabase 中不存在，已跳过。");
                return null;
            }

            return new InventorySlot(item, Mathf.Max(1, saved.Count), saved.CustomName);
        }

        /// <summary>
        /// 懒构建物品索引（ItemSO.ID -> ItemSO）。
        /// </summary>
        private bool EnsureItemIndex()
        {
            if (_itemById != null) return true;
            if (itemDatabase == null) return false;

            _itemById = DatabaseIndexer.BuildById(
                itemDatabase.Items,
                item => item != null ? item.ID : null,
                "InventoryManager");
            return true;
        }

        /// <summary>
        /// 背包存档结构：
        /// 单列表 + Location 标记（0=背包 / 1=装备武器 / 2=装备防具），
        /// 避免 JsonUtility 对 null 类字段序列化行为不可控的问题。
        /// </summary>
        [Serializable]
        private class InventorySaveState
        {
            public List<InventorySlotSaveData> Slots = new List<InventorySlotSaveData>();
        }

        /// <summary>
        /// 单个槽位的存档结构。
        /// </summary>
        [Serializable]
        private class InventorySlotSaveData
        {
            public string ItemID;
            public int Count;
            public string CustomName;
            public int Location;
        }
    }
}
