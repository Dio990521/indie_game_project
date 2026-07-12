namespace IndieGame.Gameplay.Equipment
{
    /// <summary>
    /// 装备部位类型：
    /// 驱动装备界面的 Tab 分类与"已装备"槽位归属，也用于 EquipmentItemSO 声明自己属于哪个部位。
    /// </summary>
    public enum EquipmentType
    {
        Weapon,
        Armor,
        // 配方：预留部位，系统尚未实现，暂无对应的 Controller/事件，仅用于占位 UI 与未来扩展。
        Recipe
    }
}
