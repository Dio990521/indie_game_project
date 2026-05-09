using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 连锁移位（Combo Move）系统：全局静态类，供 BoardMovementController 写入、各奖励系统读取。
    /// 使用静态类而非 MonoBehaviour，是因为 ScriptableObject 格子无法持有 MonoBehaviour 引用，
    /// 而静态成员可被任何系统（格子、战斗、采集）零依赖接入。
    /// </summary>
    public static class ComboMoveSystem
    {
        private static int _comboCount;

        /// <summary> 当前连锁次数（每次掷骰开始时归零） </summary>
        public static int ComboCount => _comboCount;

        /// <summary> 当前奖励倍率 = 1 + Combo </summary>
        public static float Multiplier => 1f + _comboCount;

        /// <summary>
        /// 每次 BeginMove 时调用，重置连锁计数并广播倍率归一事件。
        /// </summary>
        internal static void ResetCombo()
        {
            if (_comboCount == 0) return;
            _comboCount = 0;
            EventBus.Raise(new ComboMoveUpdatedEvent { ComboCount = 0, Multiplier = 1f });
        }

        /// <summary>
        /// 每触发一次位移格时调用，累计连锁计数并广播最新倍率事件。
        /// </summary>
        internal static void IncrementCombo()
        {
            _comboCount++;
            EventBus.Raise(new ComboMoveUpdatedEvent { ComboCount = _comboCount, Multiplier = Multiplier });
        }
    }
}
