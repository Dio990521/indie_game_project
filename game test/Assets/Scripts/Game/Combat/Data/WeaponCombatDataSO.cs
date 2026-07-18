using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 武器战斗数据（独立 SO，不并入 WeaponSO 字段）：
    /// - WeaponSO 通过引用本 SO 获得战斗能力，对已有武器资产零迁移（新字段默认 null）；
    /// - 敌人不走装备/背包系统，EnemyDefinitionSO 可直接引用同一份数据；
    /// - 多把武器可共享同一战斗原型（如"铁剑/银剑"共用普攻参数，仅 Modifiers 不同）。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Weapon Combat Data")]
    public class WeaponCombatDataSO : ScriptableObject
    {
        [Header("普攻")]
        [Tooltip("普攻基础伤害（最终伤害 = 基础伤害 + 攻击力 - 防御力，下限 1）")]
        public int BaseDamage = 10;

        [Tooltip("普攻间隔（秒/次），越小攻速越快")]
        public float AttackInterval = 1.2f;

        [Tooltip("普攻射程（米），近战约 2，远程约 8")]
        public float AttackRange = 2f;

        [Tooltip("是否远程武器（远程使用弹道预制体，近战直接结算）")]
        public bool IsRanged = false;

        [Tooltip("弹道预制体（IsRanged 时必填，运行时走对象池）")]
        public GameObject ProjectilePrefab;

        [Tooltip("弹道飞行速度（米/秒）")]
        public float ProjectileSpeed = 12f;

        [Header("充能")]
        [Tooltip("充能条上限，充满后可释放技能")]
        public float MaxCharge = 100f;

        [Tooltip("普攻命中一次获得的充能（攻击频率越高充能越快）")]
        public float ChargeGainPerHit = 8f;

        [Tooltip("武器自身的每秒被动充能速率（与伤害数值无关；另有角色 ChargeRate 属性加成）")]
        public float ChargePerSecond = 2f;

        [Header("技能")]
        [Tooltip("充能满后按键释放的技能（可空 = 该武器无技能）")]
        public SkillSO Skill;
    }
}
