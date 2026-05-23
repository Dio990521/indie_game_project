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
                Handlers[type] = (Action<T>)existing + handler;
                return;
            }
            // 首次订阅时创建委托链
            Handlers[type] = handler;
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
                return;
            }
            // 更新委托链
            Handlers[type] = updated;
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
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out Delegate existing)) return;
            if (existing is not Action<T> chain) return;

            // 遍历委托调用链，逐个调用并隔离异常：
            // GetInvocationList 返回的是按订阅顺序的委托数组，遍历期间即便某个处理器抛异常，
            // 也不会影响后续处理器；异常会被记录在日志中以便定位。
            Delegate[] invocationList = chain.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<T>)invocationList[i])(evt);
                }
                catch (Exception ex)
                {
                    DebugTools.LogError($"[EventBus] Handler {invocationList[i].Method.DeclaringType?.Name}.{invocationList[i].Method.Name} threw on {type.Name}: {ex}");
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
