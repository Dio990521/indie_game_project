using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Treasure;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 不动铃铛宝具激活状态：
    /// 在移动控制器上设置"不动"标志后立即返回玩家回合，等待玩家掷骰。
    /// 下一次移动将忽略所有位移格效果，强制停在骰子结果格。
    /// </summary>
    public class ImmovableBellTreasureState : BoardState
    {
        private readonly ImmovableBellTreasureSO _data;

        public ImmovableBellTreasureState(ImmovableBellTreasureSO data)
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
                DebugTools.Log("[ImmovableBellTreasureState] 行动力不足或数据缺失，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            // 在移动控制器上启用"不动"标志，效果将在下一次掷骰移动时生效
            context.movementController?.ActivateImmovableBell();

            // 消耗行动力
            ActionPointSystem.Instance.TryConsumeActionPoints(_data.ActionPointCost, "ImmovableBell");

            DebugTools.Log("[ImmovableBellTreasureState] 不动铃铛已激活，下一次移动将强制停在骰子结果格。");

            // 立即返回玩家回合，等待掷骰
            context.ChangeState(new PlayerTurnState());
        }
    }
}
