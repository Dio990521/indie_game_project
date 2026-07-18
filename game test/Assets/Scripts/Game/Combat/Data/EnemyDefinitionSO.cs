using UnityEngine;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 敌人定义：
    /// 敌人不走装备/背包系统，武器战斗数据直接引用 WeaponCombatDataSO。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Enemy Definition")]
    public class EnemyDefinitionSO : ScriptableObject
    {
        [Tooltip("敌人唯一 ID（日志/掉落配置用）")]
        public string ID;

        [Tooltip("敌人显示名")]
        public string DisplayName;

        [Tooltip("战斗体预制体：挂 CombatUnit + CharacterStats + NavMeshAgent 及各战斗组件")]
        public GameObject CombatUnitPrefab;

        [Tooltip("数值配置（生成战斗体后注入 CharacterStats）")]
        public CharacterStatConfigSO StatConfig;

        [Tooltip("武器战斗数据（普攻参数来源）")]
        public WeaponCombatDataSO WeaponData;

        [Tooltip("敌人等级（决定数值成长曲线取值）")]
        public int Level = 1;

        [Tooltip("击杀经验奖励（Phase 1 暂未发放，预留）")]
        public int ExpReward = 10;
    }
}
