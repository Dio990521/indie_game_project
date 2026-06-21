using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 武器配置：
    /// 在通用 ItemSO 基础上追加"装备后对角色属性的加成"数据。
    /// 武器本身不负责把加成应用到角色身上，那是 WeaponEquipController 的职责（单一职责）。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Inventory/Weapon")]
    public class WeaponSO : ItemSO
    {
        [Header("Equip Modifiers")]
        [Tooltip("装备该武器后施加到角色身上的属性加成，按 StatType 映射到 CharacterStats 对应的 Stat")]
        public List<StatModifierData> Modifiers = new List<StatModifierData>();
    }
}
