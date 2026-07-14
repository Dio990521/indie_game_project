using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;

namespace IndieGame.Core
{
    /// <summary>
    /// 事件总线（全局静态）：
    /// 通过类型做 Key，集中管理跨模块事件的订阅与派发，
    /// 用于降低系统之间的直接依赖。
    /// </summary>
    public static class EventBus
    {
        // 存储每种事件类型对应的委托链
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        // H3 性能修复：缓存每种事件类型的调用链快照。
        // 旧实现在每次 Raise 里调用 chain.GetInvocationList()，该调用每次分配一个新 Delegate[]，
        // 对 InputMoveEvent 这类每帧触发的事件意味着稳定的每帧 GC Alloc。
        // 现改为仅在 Subscribe/Unsubscribe 时重建快照，Raise 阶段零分配。
        // 快照语义与 GetInvocationList 一致：Raise 遍历的是"派发那一刻"的订阅者数组，
        // 处理器在回调中订阅/退订不影响本轮遍历（下一轮生效）。
        private static readonly Dictionary<Type, Delegate[]> CachedInvocationLists = new Dictionary<Type, Delegate[]>();

        // Lightweight global event hub for decoupled systems.
        /// <summary>
        /// 订阅事件：
        /// 当 EventBus.Raise<T> 被调用时，会回调所有订阅的处理器。
        /// </summary>
        /// <typeparam name="T">事件结构体类型</typeparam>
        /// <param name="handler">事件处理器</param>
        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Type type = typeof(T);
            if (Handlers.TryGetValue(type, out Delegate existing))
            {
                // 追加到委托链
                Action<T> combined = (Action<T>)existing + handler;
                Handlers[type] = combined;
                CachedInvocationLists[type] = combined.GetInvocationList();
                return;
            }
            // 首次订阅时创建委托链
            Handlers[type] = handler;
            CachedInvocationLists[type] = handler.GetInvocationList();
        }

        /// <summary>
        /// 取消订阅事件：
        /// 需要传入与 Subscribe 时完全一致的方法引用。
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out Delegate existing)) return;
            Action<T> updated = (Action<T>)existing - handler;
            if (updated == null)
            {
                // 没有任何订阅者后移除该类型键
                Handlers.Remove(type);
                CachedInvocationLists.Remove(type);
                return;
            }
            // 更新委托链与调用快照
            Handlers[type] = updated;
            CachedInvocationLists[type] = updated.GetInvocationList();
        }

        /// <summary>
        /// 派发事件：
        /// 会同步调用所有订阅该类型的处理器。
        /// <para>
        /// 单个处理器抛出的异常会被捕获并记入日志，**不会**中断后续处理器的执行。
        /// 这避免了"某个 UI 处理器异常 → 整条订阅链断裂 → 其他系统静默丢更新"的级联故障。
        /// </para>
        /// </summary>
        public static void Raise<T>(T evt)
        {
            // 直接读缓存快照：Raise 阶段零 GC 分配（详见 CachedInvocationLists 注释）
            if (!CachedInvocationLists.TryGetValue(typeof(T), out Delegate[] invocationList)) return;

            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<T>)invocationList[i])(evt);
                }
                catch (Exception ex)
                {
                    DebugTools.LogError($"[EventBus] Handler {invocationList[i].Method.DeclaringType?.Name}.{invocationList[i].Method.Name} threw on {typeof(T).Name}: {ex}");
                }
            }
        }

        /// <summary>
        /// 查询某类型事件是否有订阅者。
        /// </summary>
        public static bool HasSubscribers<T>()
        {
            Type type = typeof(T);
            return Handlers.TryGetValue(type, out Delegate existing) && existing != null;
        }
    }
}
