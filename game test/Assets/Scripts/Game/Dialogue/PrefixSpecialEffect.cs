using System;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 强化前缀的特殊效果类型：
    /// 当前项目没有战斗/状态效果系统，这些类型仅用于在 UI 上拼接说明文字（如"出血几率 15%"），
    /// 不接入任何伤害结算或状态效果模拟。
    /// </summary>
    public enum PrefixSpecialEffectType
    {
        Bleed,              // 出血
        Poison,             // 中毒
        Crit,               // 暴击提升
        AttackSpeed,        // 攻速提升
        ChargeSpeed,        // 蓄力更快
        StatusResistance    // 异常状态抗性
    }

    /// <summary>
    /// 一条特殊效果描述（数据+UI占位，不参与任何战斗计算）。
    /// </summary>
    [Serializable]
    public struct PrefixSpecialEffect
    {
        public PrefixSpecialEffectType Type;
        // 数值含义随 Type 变化：触发几率(%)或数值加成，仅用于展示
        public float Value;
    }
}
