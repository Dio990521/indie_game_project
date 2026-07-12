namespace IndieGame.Gameplay.Stats
{
    /// <summary>
    /// StatType 中文显示名工具：装备详情面板等需要展示"属性加成"文本的地方共用同一份映射，
    /// 避免各处各写一份 switch。设计上与 ItemRarityUtility 保持一致的写法。
    /// </summary>
    public static class StatTypeDisplayUtility
    {
        public static string GetDisplayName(StatType type)
        {
            return type switch
            {
                StatType.Attack     => "攻击",
                StatType.Defense    => "防御",
                StatType.Resistance => "抗性",
                StatType.MoveSpeed  => "速度",
                StatType.Luck       => "幸运",
                StatType.HP         => "生命",
                StatType.ChargeRate => "充能速率",
                _                   => "未知属性"
            };
        }
    }
}
