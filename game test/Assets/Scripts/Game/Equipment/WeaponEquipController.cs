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

        /// <summary> 当前装备的武器，未装备时为 null </summary>
        public WeaponSO CurrentWeapon { get; private set; }

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        /// <summary>
        /// 装备武器：把武器从背包移除并应用属性加成；若已有武器装备中，先把旧武器还回背包。
        /// 换装时"移除新武器"与"归还旧武器"净占用槽位数不变，必定有空间，因此不做容量校验。
        /// </summary>
        public void Equip(WeaponSO weapon)
        {
            if (weapon == null || weapon == CurrentWeapon) return;

            WeaponSO previous = CurrentWeapon;

            InventoryManager.Instance?.RemoveItem(weapon, 1);

            if (previous != null)
            {
                RemoveAppliedModifiers();
                InventoryManager.Instance?.AddItem(previous, 1);
                EventBus.Raise(new WeaponUnequippedEvent { Owner = gameObject, Weapon = previous });
            }

            CurrentWeapon = weapon;
            ApplyModifiers(weapon.Modifiers);

            EventBus.Raise(new WeaponEquippedEvent { Owner = gameObject, Weapon = weapon });
        }

        /// <summary>
        /// 卸下当前武器：撤销属性加成并把武器还回背包。
        /// 若背包已满（无法放回），则放弃卸下，避免武器在"卸下"过程中丢失。
        /// </summary>
        /// <returns>true=卸下成功；false=背包已满，卸下被拒绝</returns>
        public bool Unequip()
        {
            if (CurrentWeapon == null) return false;

            WeaponSO weapon = CurrentWeapon;
            if (InventoryManager.Instance != null && !InventoryManager.Instance.CanAddItem(weapon, 1))
            {
                DebugTools.LogWarning("[WeaponEquipController] 背包已满，无法卸下武器。");
                return false;
            }

            RemoveAppliedModifiers();
            CurrentWeapon = null;
            InventoryManager.Instance?.AddItem(weapon, 1);

            EventBus.Raise(new WeaponUnequippedEvent { Owner = gameObject, Weapon = weapon });
            return true;
        }

        private void ApplyModifiers(List<StatModifierData> modifiers)
        {
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
