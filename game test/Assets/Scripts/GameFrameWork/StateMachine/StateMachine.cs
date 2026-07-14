using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;

namespace IndieGame.Core
{
    /// <summary>
    /// 泛型状态机（无 Unity 依赖）：
    /// - 通过泛型上下文 T 传入宿主（如 BoardGameManager）
    /// - 支持状态切换、更新、清理
    /// - 支持在切换过程中的“排队切换”与“延迟清理”
    /// </summary>
    public class StateMachine<T>
    {
        /// <summary>
        /// 当前激活的状态（只读）。
        /// </summary>
        public BaseState<T> CurrentState { get; private set; }
        // 等待切换的队列（用于在切换过程中排队）
        private readonly Queue<BaseState<T>> _pendingStates = new Queue<BaseState<T>>();
        // 是否正在切换中（防止重入）
        private bool _isTransitioning;
        // 在切换过程中请求清理的标记
        private bool _pendingClear;

        // Generic FSM: no Unity dependency, context passed per call.
        /// <summary>
        /// 切换到新状态：
        /// - 如果正在切换，则排队等待
        /// - 如果是同类型状态，默认忽略
        /// - 切换完成后会处理排队队列
        /// </summary>
        public void ChangeState(BaseState<T> newState, T context)
        {
            if (newState == null) return;
            // 非切换中时：若新状态类型与当前一致，则不重复切换
            if (!_isTransitioning && CurrentState != null && CurrentState.GetType() == newState.GetType()) return;
            if (_isTransitioning)
            {
                // 切换中：把新状态加入等待队列
                EnqueuePending(newState);
                return;
            }

            // 立即执行切换
            TransitionTo(newState, context);
            // 切换完成后处理等待队列
            ProcessPending(context);
        }

        /// <summary>
        /// 更新当前状态（每帧/每次调用由外部驱动）。
        /// </summary>
        public void Update(T context)
        {
            CurrentState?.OnUpdate(context);
        }

        /// <summary>
        /// 清理状态机：
        /// - 若正在切换，则标记为“切换完成后清理”
        /// - 否则立即清理当前状态并清空队列
        /// </summary>
        public void Clear(T context)
        {
            if (_isTransitioning)
            {
                // 记录清理请求，并清空等待队列
                _pendingClear = true;
                _pendingStates.Clear();
                return;
            }
            DoClear(context);
        }

        /// <summary>
        /// 将状态加入等待队列：
        /// - 全队列去重：若队列中（任意位置）已含同类型状态，则不再入队，避免 [A, B, A] 残留；
        /// - 同类型即当前状态：若新状态与 CurrentState 同类型，也直接忽略；
        /// - 若已标记清理则不再排队；
        /// - 队列容量上限保护：超过阈值时丢弃最早入队，避免高频切换无限堆积。
        /// </summary>
        private void EnqueuePending(BaseState<T> newState)
        {
            if (_pendingClear) return;

            // 若新状态与当前一致，无需排队（避免重复切换到自身）。
            if (CurrentState != null && CurrentState.GetType() == newState.GetType()) return;

            // 全队列去重：遍历队列检查是否已有同类型，避免只看末端而漏掉队列中间的同类型。
            Type targetType = newState.GetType();
            foreach (BaseState<T> pending in _pendingStates)
            {
                if (pending != null && pending.GetType() == targetType) return;
            }

            // 队列容量上限保护：避免高频切换或调用链异常时无限堆积内存。
            // 阈值 32 是经验值：正常游戏流程下排队深度极少超过 2-3。
            const int maxPendingStates = 32;
            if (_pendingStates.Count >= maxPendingStates)
            {
                DebugTools.LogWarning($"[StateMachine] 等待队列已达上限 {maxPendingStates}，丢弃最早入队的状态以避免堆积。");
                _pendingStates.Dequeue();
            }

            _pendingStates.Enqueue(newState);
        }

        /// <summary>
        /// 执行状态切换：
        /// 触发旧状态 OnExit，再触发新状态 OnEnter。
        /// <para>
        /// 关键修复：用 try/finally 确保 _isTransitioning 在任何情况下都会复位。
        /// 否则 OnExit/OnEnter 中任意一处抛异常都会让标志卡在 true，后续所有 ChangeState
        /// 都会走 Enqueue 分支，状态机僵死，且没有任何报错路径可以恢复。
        /// </para>
        /// </summary>
        private void TransitionTo(BaseState<T> newState, T context)
        {
            try
            {
                _isTransitioning = true;
                CurrentState?.OnExit(context);
                CurrentState = newState;
                CurrentState.OnEnter(context);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// 处理等待队列：
        /// - 支持在切换中请求清理
        /// - 顺序执行队列中的切换请求
        /// </summary>
        private void ProcessPending(T context)
        {
            if (_pendingClear)
            {
                // 优先执行清理
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
                    // 若切换过程中又请求清理，立即处理
                    DoClear(context);
                    _pendingClear = false;
                }
            }
        }

        /// <summary>
        /// 实际执行清理：
        /// 触发当前状态 OnExit，并清空所有待切换状态。
        /// L3 修复：先清队列再判空——旧实现在 CurrentState == null 时提前 return，
        /// 会漏掉 _pendingStates 里残留的待切换状态。
        /// </summary>
        private void DoClear(T context)
        {
            _pendingStates.Clear();
            if (CurrentState == null) return;
            CurrentState.OnExit(context);
            CurrentState = null;
        }
    }
}
