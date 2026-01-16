namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class EventState : BoardState
    {
        public EventState(BoardGameManager context) : base(context) { }

        public override void Enter()
        {
            // 事件已在移动结束时触发（TileBase.OnPlayerStop）
            Context.ChangeState(new PlayerTurnState(Context));
        }
    }
}
