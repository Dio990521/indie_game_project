using System;

namespace IndieGame.Gameplay.Stats
{
    /// <summary>
    /// 一条属性加成数据：
    /// 描述"加成哪个属性、加多少"，由装备/Buff 等配置持有，交给统一的应用逻辑处理。
    /// </summary>
    [Serializable]
    public struct StatModifierData
    {
        // 加成的目标属性
        public StatType Type;

        // 加成数值（当前 Stat 仅支持 flat 加法，正数为增益，负数为减益）
        public float Value;
    }
}
