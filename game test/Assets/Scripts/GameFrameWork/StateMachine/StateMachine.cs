namespace IndieGame.Core
{
    public class StateMachine<T>
    {
        public BaseState<T> CurrentState { get; private set; }
        private bool _isTransitioning;

        // Generic FSM: no Unity dependency, context passed per call.
        public void ChangeState(BaseState<T> newState, T context)
        {
            if (newState == null) return;
            if (CurrentState != null && CurrentState.GetType() == newState.GetType()) return;
            if (_isTransitioning) return;

            _isTransitioning = true;
            CurrentState?.OnExit(context);
            CurrentState = newState;
            CurrentState.OnEnter(context);
            _isTransitioning = false;
        }

        public void Update(T context)
        {
            CurrentState?.OnUpdate(context);
        }

        public void Clear(T context)
        {
            if (CurrentState == null) return;
            CurrentState.OnExit(context);
            CurrentState = null;
        }
    }
}
