using UnityEngine;

namespace IndieGame.Gameplay.Stats
{
    [CreateAssetMenu(menuName = "IndieGame/Stats/Character Stat Config")]
    public class CharacterStatConfigSO : ScriptableObject
    {
        [Header("Base Stats")]
        [SerializeField] private int maxHP = 100;
        [SerializeField] private float attack = 10f;
        [SerializeField] private float defense = 5f;
        [SerializeField] private float resistance = 3f;
        [SerializeField] private float moveSpeed = 5f;

        [Header("Progression Curves")]
        [SerializeField] private AnimationCurve expToNextLevel = AnimationCurve.Linear(1f, 10f, 10f, 100f);
        [SerializeField] private AnimationCurve hpGrowth = AnimationCurve.Linear(1f, 0f, 10f, 100f);
        [SerializeField] private AnimationCurve attackGrowth = AnimationCurve.Linear(1f, 0f, 10f, 10f);
        [SerializeField] private AnimationCurve defenseGrowth = AnimationCurve.Linear(1f, 0f, 10f, 5f);
        [SerializeField] private AnimationCurve resistanceGrowth = AnimationCurve.Linear(1f, 0f, 10f, 3f);
        [SerializeField] private AnimationCurve moveSpeedGrowth = AnimationCurve.Linear(1f, 0f, 10f, 1f);

        public int MaxHP => maxHP;
        public float Attack => attack;
        public float Defense => defense;
        public float Resistance => resistance;
        public float MoveSpeed => moveSpeed;
        public AnimationCurve ExpToNextLevel => expToNextLevel;
        public AnimationCurve HPGrowth => hpGrowth;
        public AnimationCurve AttackGrowth => attackGrowth;
        public AnimationCurve DefenseGrowth => defenseGrowth;
        public AnimationCurve ResistanceGrowth => resistanceGrowth;
        public AnimationCurve MoveSpeedGrowth => moveSpeedGrowth;
    }
}
