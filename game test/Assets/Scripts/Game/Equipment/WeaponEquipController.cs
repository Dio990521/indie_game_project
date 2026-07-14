using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Equipment
{
    /// <summary>
    /// 武器装备控制器：
    /// 把 WeaponSO.Modifiers 应用到角色的 CharacterStats 上，卸下时精确撤销，不影响其他来源
    /// （Buff、其他装备）产生的加成；同时负责"装备后从背包移除、卸下后还回背包"的库存联动，
    /// 避免武器同时存在于"已装备"和"背包"两个地方。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
    public class WeaponEquipController : MonoBehaviour
    {
        private CharacterStats _stats;

        // 当前已应用的加成快照：装备时记录，卸下时按记录逐条撤销，而不是直接清空 Stat
        private readonly List<StatModifierData> _appliedModifiers = new List<StatModifierData>();

        /// <summary> 当前装备的武器槽位（含 CustomName、强化数据），未装备时为 null </summary>
        public InventorySlot CurrentWeaponSlot { get; private set; }

        /// <summary> 当前装备的武器配置，未装备时为 null（派生自 CurrentWeaponSlot，兼容旧调用点） </summary>
        public WeaponSO CurrentWeapon => CurrentWeaponSlot?.Item as WeaponSO;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        /// <summary>
        /// 装备武器：把整个槽位对象从背包摘出并应用属性加成（基础 Modifiers + 已应用前缀的加成）；
        /// 若已有武器装备中，先把旧武器槽位原样还回背包。换装时"摘出新武器"与"归还旧武器"
        /// 净占用槽位数不变，必定有空间，因此不做容量校验。
        /// </summary>
        /// <returns>true=装备成功；false=参数非法（slot 为空/不是武器/已是当前装备）</returns>
        public bool Equip(InventorySlot weaponSlot)
        {
            WeaponSO weapon = weaponSlot?.Item as WeaponSO;
            if (weapon == null || weaponSlot == CurrentWeaponSlot) return false;

            InventorySlot previous = CurrentWeaponSlot;

            InventoryManager.Instance?.RemoveSlot(weaponSlot);

            if (previous != null)
            {
                RemoveAppliedModifiers();
                InventoryManager.Instance?.InsertSlot(previous);
                EventBus.Raise(new WeaponUnequippedEvent { Owner = gameObject, Weapon = previous.Item as WeaponSO });
            }

            CurrentWeaponSlot = weaponSlot;
            ApplyCurrentModifiers();

            EventBus.Raise(new WeaponEquippedEvent { Owner = gameObject, Weapon = weapon });
            return true;
        }

        /// <summary>
        /// 卸下当前武器：撤销属性加成并把武器槽位原样还回背包（保留 CustomName/强化数据）。
        /// 若背包已满（无法放回），则放弃卸下，避免武器在"卸下"过程中丢失。
        /// </summary>
        /// <returns>true=卸下成功；false=背包已满，卸下被拒绝</returns>
        public bool Unequip()
        {
            if (CurrentWeaponSlot == null) return false;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null && !inventory.CanInsertSlot())
            {
                DebugTools.LogWarning("[WeaponEquipController] 背包已满，无法卸下武器。");
                return false;
            }

            RemoveAppliedModifiers();

            InventorySlot removed = CurrentWeaponSlot;
            CurrentWeaponSlot = null;
            inventory?.InsertSlot(removed);

            EventBus.Raise(new WeaponUnequippedEvent { Owner = gameObject, Weapon = removed.Item as WeaponSO });
            return true;
        }

        /// <summary>
        /// 存档恢复专用入口（仅供 InventoryManager 读档流程调用）：
        /// 与 Equip 不同，本方法不做背包摘出/归还联动——读档时槽位由存档直接重建，
        /// 既不在背包里、也不需要把"旧装备"还回背包（旧装备属于被覆盖的过期状态）。
        /// 传入 null 表示"该存档没有装备"，会清空当前装备与其加成。
        /// </summary>
        public void RestoreEquipped(InventorySlot weaponSlot)
        {
            WeaponSO previous = CurrentWeapon;

            RemoveAppliedModifiers();
            CurrentWeaponSlot = (weaponSlot != null && weaponSlot.Item is WeaponSO) ? weaponSlot : null;
            ApplyCurrentModifiers();

            // 事件广播与 Equip/Unequip 保持一致，让 UI（背包/属性面板）正常刷新
            if (previous != null && CurrentWeapon == null)
            {
                EventBus.Raise(new WeaponUnequippedEvent { Owner = gameObject, Weapon = previous });
            }
            if (CurrentWeapon != null)
            {
                EventBus.Raise(new WeaponEquippedEvent { Owner = gameObject, Weapon = CurrentWeapon });
            }
        }

        /// <summary>
        /// 重新计算并应用当前武器的加成（基础 Modifiers）。
        /// </summary>
        public void RefreshAppliedModifiers()
        {
            if (CurrentWeaponSlot == null) return;
            RemoveAppliedModifiers();
            ApplyCurrentModifiers();
        }

        /// <summary>
        /// 应用当前武器的基础 Modifiers。
        /// </summary>
        private void ApplyCurrentModifiers()
        {
            WeaponSO weapon = CurrentWeapon;
            if (weapon == null) return;

            List<StatModifierData> modifiers = weapon.Modifiers;

            for (int i = 0; i < modifiers.Count; i++)
            {
                Stat stat = _stats.GetStat(modifiers[i].Type);
                if (stat == null) continue;

                stat.AddModifier(modifiers[i].Value);
                _appliedModifiers.Add(modifiers[i]);
            }
        }

        private void RemoveAppliedModifiers()
        {
            for (int i = 0; i < _appliedModifiers.Count; i++)
            {
                Stat stat = _stats.GetStat(_appliedModifiers[i].Type);
                stat?.RemoveModifier(_appliedModifiers[i].Value);
            }
            _appliedModifiers.Clear();
        }
    }
}
