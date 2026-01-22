using System.Collections.Generic;
using System.Linq;

namespace IndieGame.Core
{
    public class StateMachine<T>
    {
        public BaseState<T> CurrentState { get; private set; }
        private readonly Queue<BaseState<T>> _pendingStates = new Queue<BaseState<T>>();
        private bool _isTransitioning;
        private bool _pendingClear;

        // Generic FSM: no Unity dependency, context passed per call.
        public void ChangeState(BaseState<T> newState, T context)
        {
            if (newState == null) return;
            if (!_isTransitioning && CurrentState != null && CurrentState.GetType() == newState.GetType()) return;
            if (_isTransitioning)
            {
                EnqueuePending(newState);
                return;
            }

            TransitionTo(newState, context);
            ProcessPending(context);
        }

        public void Update(T context)
        {
            CurrentState?.OnUpdate(context);
        }

        public void Clear(T context)
        {
            if (_isTransitioning)
            {
                _pendingClear = true;
                _pendingStates.Clear();
                return;
            }
            DoClear(context);
        }

        private void EnqueuePending(BaseState<T> newState)
        {
            if (_pendingClear) return;
            if (_pendingStates.Count == 0 && CurrentState != null && CurrentState.GetType() == newState.GetType()) return;
            if (_pendingStates.Count > 0 && _pendingStates.Last().GetType() == newState.GetType()) return;
            _pendingStates.Enqueue(newState);
        }

        private void TransitionTo(BaseState<T> newState, T context)
        {
            _isTransitioning = true;
            CurrentState?.OnExit(context);
            CurrentState = newState;
            CurrentState.OnEnter(context);
            _isTransitioning = false;
        }

        private void ProcessPending(T context)
        {
            if (_pendingClear)
            {
                DoClear(context);
                _pendingClear = false;
            }

            while (_pendingStates.Count > 0)
            {
                BaseState<T> nextState = _pendingStates.Dequeue();
                if (nextState == null) continue;
                if (CurrentState != null && CurrentState.GetType() == nextState.GetType()) continue;
                TransitionTo(nextState, context);

                if (_pendingClear)
                {
                    DoClear(context);
                    _pendingClear = false;
                }
            }
        }

        private void DoClear(T context)
        {
            if (CurrentState == null) return;
            CurrentState.OnExit(context);
            CurrentState = null;
            _pendingStates.Clear();
        }
    }
}
