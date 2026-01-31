using UnityEngine;

namespace IndieGame.Gameplay.Stats
{
    [CreateAssetMenu(menuName = "IndieGame/Stats/Character Stat Config")]
    public class CharacterStatConfigSO : ScriptableObject
    {
        [Header("Base Stats")]
        // --- 基础属性（Level 1 的起始值） ---
        [SerializeField] private int maxHP = 100;
        [SerializeField] private float attack = 10f;
        [SerializeField] private float defense = 5f;
        [SerializeField] private float resistance = 3f;
        [SerializeField] private float moveSpeed = 5f;

        [Header("Progression Curves")]
        // --- 成长曲线（根据等级叠加的增量） ---
        [SerializeField] private AnimationCurve expToNextLevel = AnimationCurve.Linear(1f, 10f, 10f, 100f);
        [SerializeField] private AnimationCurve hpGrowth = AnimationCurve.Linear(1f, 0f, 10f, 100f);
        [SerializeField] private AnimationCurve attackGrowth = AnimationCurve.Linear(1f, 0f, 10f, 10f);
        [SerializeField] private AnimationCurve defenseGrowth = AnimationCurve.Linear(1f, 0f, 10f, 5f);
        [SerializeField] private AnimationCurve resistanceGrowth = AnimationCurve.Linear(1f, 0f, 10f, 3f);
        [SerializeField] private AnimationCurve moveSpeedGrowth = AnimationCurve.Linear(1f, 0f, 10f, 1f);

        /// <summary> 初始最大生命 </summary>
        public int MaxHP => maxHP;
        /// <summary> 初始攻击力 </summary>
        public float Attack => attack;
        /// <summary> 初始防御力 </summary>
        public float Defense => defense;
        /// <summary> 初始抗性 </summary>
        public float Resistance => resistance;
        /// <summary> 初始移动速度 </summary>
        public float MoveSpeed => moveSpeed;
        /// <summary> 升级所需经验曲线 </summary>
        public AnimationCurve ExpToNextLevel => expToNextLevel;
        /// <summary> 生命成长曲线 </summary>
        public AnimationCurve HPGrowth => hpGrowth;
        /// <summary> 攻击成长曲线 </summary>
        public AnimationCurve AttackGrowth => attackGrowth;
        /// <summary> 防御成长曲线 </summary>
        public AnimationCurve DefenseGrowth => defenseGrowth;
        /// <summary> 抗性成长曲线 </summary>
        public AnimationCurve ResistanceGrowth => resistanceGrowth;
        /// <summary> 移速成长曲线 </summary>
        public AnimationCurve MoveSpeedGrowth => moveSpeedGrowth;
    }
}
