using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗数值公式（纯静态、无副作用，便于单元测试）：
    /// 所有伤害/充能计算集中在此，避免公式散落在各组件里。
    /// </summary>
    public static class CombatFormulas
    {
        /// <summary>
        /// 伤害公式：基础伤害 + 攻击力 - 防御力，下限 1。
        /// </summary>
        /// <param name="baseDamage">武器/技能基础伤害</param>
        /// <param name="attack">攻击方攻击力</param>
        /// <param name="defense">受击方防御力</param>
        public static int CalculateDamage(int baseDamage, int attack, int defense)
        {
            return Mathf.Max(1, baseDamage + attack - defense);
        }

        /// <summary>
        /// 每秒被动充能速率 = 武器自身速率 + 角色 ChargeRate 属性加成（与伤害数值无关）。
        /// </summary>
        /// <param name="weaponChargePerSecond">武器每秒充能</param>
        /// <param name="statChargeRate">角色 ChargeRate 属性值（装备/Buff 可加成）</param>
        public static float CalculatePassiveChargePerSecond(float weaponChargePerSecond, float statChargeRate)
        {
            return Mathf.Max(0f, weaponChargePerSecond + statChargeRate);
        }
    }
}
