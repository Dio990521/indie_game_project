namespace IndieGame.Gameplay.Board.Runtime.States
{
    public abstract class BoardState
    {
        protected readonly BoardGameManager Context;

        protected BoardState(BoardGameManager context)
        {
            Context = context;
        }

        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Update() { }
        public virtual void OnInteract() { }
    }
}
