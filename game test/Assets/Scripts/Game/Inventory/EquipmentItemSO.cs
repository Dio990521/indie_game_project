using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Stats;
using IndieGame.Gameplay.Equipment;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 可装备物品的公共基类：
    /// 在通用 ItemSO 基础上追加"所属装备部位"与"装备后对角色属性的加成"数据。
    /// 装备本身不负责把加成应用到角色身上，那是各部位 EquipController（如 WeaponEquipController、
    /// ArmorEquipController）的职责（单一职责）。
    /// </summary>
    public abstract class EquipmentItemSO : ItemSO
    {
        [Header("Equipment Slot")]
        [Tooltip("该装备所属的部位，决定它会出现在装备界面的哪个 Tab、占用哪个已装备槽位")]
        public EquipmentType SlotType = EquipmentType.Weapon;

        [Header("Equip Modifiers")]
        [Tooltip("装备后施加到角色身上的属性加成，按 StatType 映射到 CharacterStats 对应的 Stat")]
        public List<StatModifierData> Modifiers = new List<StatModifierData>();
    }
}
