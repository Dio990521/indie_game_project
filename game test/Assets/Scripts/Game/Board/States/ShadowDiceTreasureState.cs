using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Treasure;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 影骰子宝具激活状态：
    /// 在移动控制器上设置"影骰子"标志后立即返回玩家回合，等待玩家掷骰。
    /// 下一次掷骰子点数将翻倍，效果一次性消耗。
    /// </summary>
    public class ShadowDiceTreasureState : BoardState
    {
        private readonly ShadowDiceTreasureSO _data;

        public ShadowDiceTreasureState(ShadowDiceTreasureSO data)
        {
            _data = data;
        }

        public override void OnEnter(BoardGameManager context)
        {
            // 行动力二次检查（防止极端情况下数值被其他系统提前消耗）
            if (_data == null ||
                ActionPointSystem.Instance == null ||
                !ActionPointSystem.Instance.CanConsume(_data.ActionPointCost))
            {
                DebugTools.Log("[ShadowDiceTreasureState] 行动力不足或数据缺失，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            // 在移动控制器上启用"影骰子"标志，效果将在下一次掷骰时生效
            context.movementController?.ActivateShadowDice();

            // 消耗行动力
            ActionPointSystem.Instance.TryConsumeActionPoints(_data.ActionPointCost, "ShadowDice");

            DebugTools.Log("[ShadowDiceTreasureState] 影骰子已激活，下一次掷骰子点数将翻倍。");

            // 立即返回玩家回合，等待掷骰
            context.ChangeState(new PlayerTurnState());
        }
    }
}
