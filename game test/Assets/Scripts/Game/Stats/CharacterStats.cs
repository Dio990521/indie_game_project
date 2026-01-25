using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Stats
{
    public class CharacterStats : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private CharacterStatConfigSO config;

        [Header("Runtime Resources")]
        [SerializeField] private int currentHP;
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int currentEXP;

        public Stat Attack { get; private set; } = new Stat();
        public Stat Defense { get; private set; } = new Stat();
        public Stat Resistance { get; private set; } = new Stat();
        public Stat MoveSpeed { get; private set; } = new Stat();

        public int CurrentHP => currentHP;
        public int CurrentLevel => currentLevel;
        public int CurrentEXP => currentEXP;
        public int MaxHP => config != null ? Mathf.Max(1, Mathf.RoundToInt(GetBaseHP())) : 1;

        private void Awake()
        {
            InitializeFromConfig();
        }

        private void InitializeFromConfig()
        {
            if (config == null)
            {
                Debug.LogWarning("[CharacterStats] Missing config, using defaults.");
                Attack.BaseValue = 0f;
                Defense.BaseValue = 0f;
                Resistance.BaseValue = 0f;
                MoveSpeed.BaseValue = 5f;
                currentHP = 1;
                currentLevel = 1;
                currentEXP = 0;
                return;
            }

            ApplyBaseStatsForLevel(currentLevel);
            currentHP = MaxHP;
            currentEXP = 0;
            NotifyAll();
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            currentHP = Mathf.Max(0, currentHP - amount);
            RaiseHealthChanged();
            if (currentHP <= 0)
            {
                EventBus.Raise(new DeathEvent { Owner = gameObject });
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            currentHP = Mathf.Min(MaxHP, currentHP + amount);
            RaiseHealthChanged();
        }

        public void AddExp(int amount)
        {
            if (amount <= 0 || config == null) return;
            currentEXP += amount;
            int required = GetRequiredExp(currentLevel);

            while (currentEXP >= required)
            {
                currentEXP -= required;
                currentLevel++;
                ApplyBaseStatsForLevel(currentLevel);
                currentHP = MaxHP;
                EventBus.Raise(new LevelChangedEvent { Owner = gameObject, Level = currentLevel });
                required = GetRequiredExp(currentLevel);
            }

            RaiseExpChanged(required);
            RaiseHealthChanged();
        }

        private void ApplyBaseStatsForLevel(int level)
        {
            Attack.BaseValue = config.Attack + config.AttackGrowth.Evaluate(level);
            Defense.BaseValue = config.Defense + config.DefenseGrowth.Evaluate(level);
            Resistance.BaseValue = config.Resistance + config.ResistanceGrowth.Evaluate(level);
            MoveSpeed.BaseValue = config.MoveSpeed + config.MoveSpeedGrowth.Evaluate(level);
        }

        private float GetBaseHP()
        {
            if (config == null) return 1f;
            return config.MaxHP + config.HPGrowth.Evaluate(currentLevel);
        }

        private int GetRequiredExp(int level)
        {
            if (config == null) return int.MaxValue;
            float value = config.ExpToNextLevel.Evaluate(level);
            if (value <= 1f) return 1;
            return Mathf.RoundToInt(value);
        }

        private void NotifyAll()
        {
            RaiseHealthChanged();
            RaiseExpChanged(GetRequiredExp(currentLevel));
            EventBus.Raise(new LevelChangedEvent { Owner = gameObject, Level = currentLevel });
        }

        private void RaiseHealthChanged()
        {
            EventBus.Raise(new HealthChangedEvent
            {
                Owner = gameObject,
                Current = currentHP,
                Max = MaxHP
            });
        }

        private void RaiseExpChanged(int required)
        {
            EventBus.Raise(new ExpChangedEvent
            {
                Owner = gameObject,
                Current = currentEXP,
                Required = required
            });
        }
    }
}
