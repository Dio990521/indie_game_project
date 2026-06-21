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
        Luck
    }
}
