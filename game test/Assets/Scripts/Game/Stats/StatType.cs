namespace IndieGame.Gameplay.Stats
{
    /// <summary>
    /// 可被加成系统识别的属性类型：
    /// 装备、Buff 等数据源用它来声明"加成哪个属性"，避免对 CharacterStats 的字段名硬编码。
    /// </summary>
    public enum StatType
    {
        Attack,
        Defense,
        Resistance,
        MoveSpeed,
        Luck,
        // 角色最大生命的额外加成（叠加在 CharacterStats.MaxHP 的曲线基础值之上）
        HP,
        // 武器充能速率：当前无蓄力/技能系统读取，仅作为可加成数值占位
        ChargeRate
    }
}
