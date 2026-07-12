using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Equipment
{
    /// <summary>
    /// 防具装备控制器：
    /// 把 ArmorSO.Modifiers 应用到角色的 CharacterStats 上，卸下时精确撤销，不影响其他来源
    /// （Buff、其他装备）产生的加成；同时负责"装备后从背包移除、卸下后还回背包"的库存联动，
    /// 避免防具同时存在于"已装备"和"背包"两个地方。
    /// 结构镜像 WeaponEquipController，但不涉及强化系统（防具没有强化玩法）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
    public class ArmorEquipController : MonoBehaviour
    {
        private CharacterStats _stats;

        // 当前已应用的加成快照：装备时记录，卸下时按记录逐条撤销，而不是直接清空 Stat
        private readonly List<StatModifierData> _appliedModifiers = new List<StatModifierData>();

        /// <summary> 当前装备的防具槽位（含 CustomName），未装备时为 null </summary>
        public InventorySlot CurrentArmorSlot { get; private set; }

        /// <summary> 当前装备的防具配置，未装备时为 null（派生自 CurrentArmorSlot） </summary>
        public ArmorSO CurrentArmor => CurrentArmorSlot?.Item as ArmorSO;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        /// <summary>
        /// 装备防具：把整个槽位对象从背包摘出并应用属性加成；若已有防具装备中，先把旧防具槽位
        /// 原样还回背包。换装时"摘出新防具"与"归还旧防具"净占用槽位数不变，必定有空间，因此不做容量校验。
        /// </summary>
        /// <returns>true=装备成功；false=参数非法（slot 为空/不是防具/已是当前装备）</returns>
        public bool Equip(InventorySlot armorSlot)
        {
            ArmorSO armor = armorSlot?.Item as ArmorSO;
            if (armor == null || armorSlot == CurrentArmorSlot) return false;

            InventorySlot previous = CurrentArmorSlot;

            InventoryManager.Instance?.RemoveSlot(armorSlot);

            if (previous != null)
            {
                RemoveAppliedModifiers();
                InventoryManager.Instance?.InsertSlot(previous);
                EventBus.Raise(new ArmorUnequippedEvent { Owner = gameObject, Armor = previous.Item as ArmorSO });
            }

            CurrentArmorSlot = armorSlot;
            ApplyCurrentModifiers();

            EventBus.Raise(new ArmorEquippedEvent { Owner = gameObject, Armor = armor });
            return true;
        }

        /// <summary>
        /// 卸下当前防具：撤销属性加成并把防具槽位原样还回背包（保留 CustomName）。
        /// 若背包已满（无法放回），则放弃卸下，避免防具在"卸下"过程中丢失。
        /// </summary>
        /// <returns>true=卸下成功；false=背包已满，卸下被拒绝</returns>
        public bool Unequip()
        {
            if (CurrentArmorSlot == null) return false;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null && !inventory.CanInsertSlot())
            {
                DebugTools.LogWarning("[ArmorEquipController] 背包已满，无法卸下防具。");
                return false;
            }

            RemoveAppliedModifiers();

            InventorySlot removed = CurrentArmorSlot;
            CurrentArmorSlot = null;
            inventory?.InsertSlot(removed);

            EventBus.Raise(new ArmorUnequippedEvent { Owner = gameObject, Armor = removed.Item as ArmorSO });
            return true;
        }

        /// <summary>
        /// 应用当前防具的基础 Modifiers。
        /// </summary>
        private void ApplyCurrentModifiers()
        {
            ArmorSO armor = CurrentArmor;
            if (armor == null) return;

            List<StatModifierData> modifiers = armor.Modifiers;
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
