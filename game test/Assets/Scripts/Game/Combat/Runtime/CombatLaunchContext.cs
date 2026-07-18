using System;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗启动载荷（静态类，跨场景传递）：
    /// 棋盘/剧情等入口在调用 SceneLoader 加载战斗场景前写入本载荷；
    /// CombatInitState 读取后清空。战斗场景直接 Play（无载荷）时，
    /// 由 CombatTestBootstrapper 用兜底遭遇写入独立测试载荷。
    /// </summary>
    public static class CombatLaunchContext
    {
        /// <summary> 是否有待消费的战斗载荷 </summary>
        public static bool HasPending { get; private set; }

        /// <summary> 本场战斗的遭遇配置 </summary>
        public static EncounterSO Encounter { get; private set; }

        /// <summary> 是否为独立测试模式（战斗结束后停留在结算，不返回棋盘） </summary>
        public static bool IsStandaloneTest { get; private set; }

        /// <summary>
        /// 战斗结束回调（Phase 2 棋盘桥接用）：
        /// 参数为是否胜利。棋盘交互事件的 OnCompleted 续接逻辑挂在这里。
        /// </summary>
        public static Action<bool> OnBattleFinished { get; private set; }

        /// <summary>
        /// 正式流程入口：写入遭遇与结束回调后再加载战斗场景。
        /// </summary>
        public static void SetEncounter(EncounterSO encounter, Action<bool> onBattleFinished = null)
        {
            Encounter = encounter;
            OnBattleFinished = onBattleFinished;
            IsStandaloneTest = false;
            HasPending = encounter != null;
        }

        /// <summary>
        /// 独立测试入口：战斗场景直接 Play 时由测试引导写入兜底遭遇。
        /// </summary>
        public static void SetStandaloneTest(EncounterSO fallbackEncounter)
        {
            Encounter = fallbackEncounter;
            OnBattleFinished = null;
            IsStandaloneTest = true;
            HasPending = fallbackEncounter != null;
        }

        /// <summary>
        /// 消费载荷（CombatInitState 读取后调用；IsStandaloneTest 标记保留给结算分支判断）。
        /// </summary>
        public static void Consume()
        {
            HasPending = false;
        }

        /// <summary>
        /// 完全清空（战斗退出时调用）。
        /// </summary>
        public static void Clear()
        {
            HasPending = false;
            Encounter = null;
            OnBattleFinished = null;
            IsStandaloneTest = false;
        }
    }
}
