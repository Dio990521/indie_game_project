using UnityEngine;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 防具配置：
    /// 装备部位与属性加成字段由基类 EquipmentItemSO 提供。
    /// 防具本身不负责把加成应用到角色身上，那是 ArmorEquipController 的职责（单一职责）。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Inventory/Armor")]
    public class ArmorSO : EquipmentItemSO
    {
        // 在编辑器中新建该资源或点击 Reset 时，自动把部位设为 Armor，避免每次手动选择。
        private void Reset()
        {
            SlotType = IndieGame.Gameplay.Equipment.EquipmentType.Armor;
        }
    }
}
