using IndieGame.Core; // 确保引用了基类所在的命名空间

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class EventState : BoardState
    {
        private System.Action<IndieGame.UI.Confirmation.ConfirmationRespondedEvent> _onResponded;
        private bool _shouldSkip = false; // 新增标志位

        public override void OnEnter(BoardGameManager context)
        {
            // 检查是否有挂起的确认事件
            if (!IndieGame.UI.Confirmation.ConfirmationEvent.HasPending)
            {
                // 没有弹窗则直接跳过事件阶段
                _shouldSkip = true;
                return;
            }

            // 如果有事件，注册回调等待响应
            // 响应后再进入敌方回合，确保事件完整结束
            _onResponded = _ => context.ChangeState(new EnemyTurnState());
            EventBus.Subscribe(_onResponded);
        }

        // 添加 OnUpdate 来处理自动跳转
        public override void OnUpdate(BoardGameManager context)
        {
            if (_shouldSkip)
            {
                // 延后一帧跳转，保证状态机稳定
                _shouldSkip = false;
                context.ChangeState(new EnemyTurnState());
            }
        }

        public override void OnExit(BoardGameManager context)
        {
            // 清理事件监听
            if (_onResponded != null)
            {
                EventBus.Unsubscribe(_onResponded);
            }
            _onResponded = null;
            _shouldSkip = false;
        }
    }
}
