using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Stats
{
    /// <summary>
    /// 角色属性组件：
    /// 负责角色的基础属性、等级、经验与生命值管理，并通过 EventBus 广播数值变更。
    /// </summary>
    public class CharacterStats : MonoBehaviour, IDamageable
    {
        [Header("Config")]
        // 角色数值配置（包含初始值与成长曲线）
        [SerializeField] private CharacterStatConfigSO config;

        [Header("Runtime Resources")]
        // --- 运行时动态值 ---
        [SerializeField] private int currentHP;
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int currentEXP;

        // --- 可加成的数值属性 ---
        public Stat Attack { get; private set; } = new Stat();
        public Stat Defense { get; private set; } = new Stat();
        public Stat Resistance { get; private set; } = new Stat();
        public Stat MoveSpeed { get; private set; } = new Stat();

        // --- 对外只读的运行时数据 ---
        public int CurrentHP => currentHP;
        public int CurrentLevel => currentLevel;
        public int CurrentEXP => currentEXP;
        // 最大生命值由基础值 + 成长曲线决定，至少为 1
        public int MaxHP => config != null ? Mathf.Max(1, Mathf.RoundToInt(GetBaseHP())) : 1;

        /// <summary>
        /// 应用“来自存档”的运行时属性状态：
        /// 该方法是 PlayerStatsSaveable 的唯一写回入口，目的是把“恢复流程”收敛到一处，
        /// 避免外部脚本直接改私有字段导致数值与事件通知不一致。
        ///
        /// 恢复顺序说明：
        /// 1) 先恢复等级（影响基础属性与最大生命）；
        /// 2) 再恢复经验；
        /// 3) 最后按恢复后的 MaxHP 对当前生命做 Clamp；
        /// 4) 统一广播一次全量事件，让 UI 与依赖系统同步。
        /// </summary>
        public void ApplySavedRuntimeState(int savedHP, int savedLevel, int savedEXP)
        {
            currentLevel = Mathf.Max(1, savedLevel);
            currentEXP = Mathf.Max(0, savedEXP);

            if (config != null)
            {
                // 有配置时，按恢复后的等级重算基础属性（攻击/防御/抗性/移速）。
                ApplyBaseStatsForLevel(currentLevel);
                // 恢复 HP 时必须使用“新等级对应的 MaxHP”做约束，避免越界。
                currentHP = Mathf.Clamp(savedHP, 0, MaxHP);
            }
            else
            {
                // 无配置兜底：无法按等级重算成长，只能保守恢复为非负生命。
                currentHP = Mathf.Max(0, savedHP);
            }

            NotifyAll();
        }

        private void Awake()
        {
            // 初始化属性（从配置加载或使用默认值）
            InitializeFromConfig();
        }

        private void InitializeFromConfig()
        {
            if (config == null)
            {
                Debug.LogWarning("[CharacterStats] Missing config, using defaults.");
                // 兜底默认值，避免空引用
                Attack.BaseValue = 0f;
                Defense.BaseValue = 0f;
                Resistance.BaseValue = 0f;
                MoveSpeed.BaseValue = 5f;
                currentHP = 1;
                currentLevel = 1;
                currentEXP = 0;
                return;
            }

            // 按当前等级应用基础数值
            ApplyBaseStatsForLevel(currentLevel);
            // 满血并清空经验
            currentHP = MaxHP;
            currentEXP = 0;
            // 广播一次全量刷新
            NotifyAll();
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            // 扣血并广播变更
            currentHP = Mathf.Max(0, currentHP - amount);
            RaiseHealthChanged();
            if (currentHP <= 0)
            {
                // 生命值归零后广播死亡事件
                EventBus.Raise(new DeathEvent { Owner = gameObject });
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            // 回复生命并广播变更
            currentHP = Mathf.Min(MaxHP, currentHP + amount);
            RaiseHealthChanged();
        }

        public void AddExp(int amount)
        {
            if (amount <= 0 || config == null) return;
            // 累加经验并尝试升级
            currentEXP += amount;
            int required = GetRequiredExp(currentLevel);

            while (currentEXP >= required)
            {
                // 升级并重置当前经验
                currentEXP -= required;
                currentLevel++;
                ApplyBaseStatsForLevel(currentLevel);
                // 升级后回满血
                currentHP = MaxHP;
                EventBus.Raise(new LevelChangedEvent { Owner = gameObject, Level = currentLevel });
                required = GetRequiredExp(currentLevel);
            }

            // 广播经验与生命（最大生命可能变化）
            RaiseExpChanged(required);
            RaiseHealthChanged();
        }

        private void ApplyBaseStatsForLevel(int level)
        {
            // 基础值 = 初始值 + 成长曲线的增量
            Attack.BaseValue = config.Attack + config.AttackGrowth.Evaluate(level);
            Defense.BaseValue = config.Defense + config.DefenseGrowth.Evaluate(level);
            Resistance.BaseValue = config.Resistance + config.ResistanceGrowth.Evaluate(level);
            MoveSpeed.BaseValue = config.MoveSpeed + config.MoveSpeedGrowth.Evaluate(level);
        }

        private float GetBaseHP()
        {
            if (config == null) return 1f;
            // 基础生命 = 初始生命 + 成长曲线增量
            return config.MaxHP + config.HPGrowth.Evaluate(currentLevel);
        }

        private int GetRequiredExp(int level)
        {
            if (config == null) return int.MaxValue;
            // 经验曲线可能返回小数，统一取整并保证最小为 1
            float value = config.ExpToNextLevel.Evaluate(level);
            if (value <= 1f) return 1;
            return Mathf.RoundToInt(value);
        }

        private void NotifyAll()
        {
            // 初始化时广播全部数值
            RaiseHealthChanged();
            RaiseExpChanged(GetRequiredExp(currentLevel));
            EventBus.Raise(new LevelChangedEvent { Owner = gameObject, Level = currentLevel });
        }

        private void RaiseHealthChanged()
        {
            // 广播生命变化
            EventBus.Raise(new HealthChangedEvent
            {
                Owner = gameObject,
                Current = currentHP,
                Max = MaxHP
            });
        }

        private void RaiseExpChanged(int required)
        {
            // 广播经验变化
            EventBus.Raise(new ExpChangedEvent
            {
                Owner = gameObject,
                Current = currentEXP,
                Required = required
            });
        }
    }
}
