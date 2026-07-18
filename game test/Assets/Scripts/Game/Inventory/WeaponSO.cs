using UnityEngine;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 武器配置：
    /// 装备部位与属性加成字段由基类 EquipmentItemSO 提供。
    /// 武器本身不负责把加成应用到角色身上，那是 WeaponEquipController 的职责（单一职责）。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Inventory/Weapon")]
    public class WeaponSO : EquipmentItemSO
    {
        [Header("Combat")]
        [Tooltip("武器战斗数据（普攻/充能/技能参数）。为空时战斗系统使用角色定义里的默认武器数据兜底")]
        public IndieGame.Gameplay.Combat.WeaponCombatDataSO CombatData;

        // 在编辑器中新建该资源或点击 Reset 时，自动把部位设为 Weapon，避免每次手动选择。
        private void Reset()
        {
            SlotType = IndieGame.Gameplay.Equipment.EquipmentType.Weapon;
        }
    }
}
