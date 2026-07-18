using IndieGame.Core;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 战斗退出状态：
    /// 触发战斗结束回调（Phase 2 棋盘桥接）、清空启动载荷；
    /// 正常流程调 SceneLoader.ReturnToBoard 返回棋盘（卸载战斗场景），
    /// 独立测试模式停留在结算画面（可用 CombatManager 的 ContextMenu 重新开战）。
    /// </summary>
    public class CombatExitState : CombatState
    {
        public override void OnEnter(CombatManager context)
        {
            bool standalone = CombatLaunchContext.IsStandaloneTest;
            var onFinished = CombatLaunchContext.OnBattleFinished;
            bool victory = context.LastBattleVictory;

            CombatLaunchContext.Clear();

            // 通知战斗发起方（棋盘桥接等）——回调在载荷清空后触发，避免回调内重入
            onFinished?.Invoke(victory);

            if (standalone)
            {
                DebugTools.Log("<color=orange>[Combat] 独立测试模式：战斗结束，停留在战斗场景。" +
                               "可通过 CombatManager 的 ContextMenu「重新开始战斗」再来一局。</color>");
                return;
            }

            if (SceneLoader.Instance != null)
            {
                DebugTools.Log("<color=orange>[Combat] 战斗结束，返回棋盘。</color>");
                SceneLoader.Instance.ReturnToBoard();
            }
            else
            {
                DebugTools.LogWarning("[Combat] SceneLoader 不可用，无法返回棋盘。");
            }
        }
    }
}
