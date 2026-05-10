using System;
using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Core
{
    /// <summary>
    /// EventBus 自动订阅/取消订阅基类：
    /// 各 UI Controller / View / Binder 中都重复出现以下模式：
    ///   OnEnable -> EventBus.Subscribe<A>(...); EventBus.Subscribe<B>(...); ...
    ///   OnDisable -> EventBus.Unsubscribe<A>(...); EventBus.Unsubscribe<B>(...); ...
    /// 本基类把这套样板抽离：
    /// - 子类在 Bind() 中调用 Subscribe&lt;T&gt;(handler) 注册事件；
    /// - OnDisable 时基类自动调用所有注册的反注册闭包，避免遗漏。
    ///
    /// 适用范围：
    /// - 适合“OnEnable -> 订阅 / OnDisable -> 取消订阅”这种生命周期内一次性绑定的脚本；
    /// - 不适合“按状态机进入/离开时分别订阅”这种动态订阅，需要保留原写法。
    /// </summary>
    public abstract class EventBusMonoBehaviour : MonoBehaviour
    {
        // 存储每个 Subscribe 调用对应的反注册闭包，OnDisable 时统一调用
        private readonly List<Action> _unsubscribers = new List<Action>();

        protected virtual void OnEnable() => Bind();
        protected virtual void OnDisable() => Unbind();

        /// <summary>
        /// 子类在此调用 Subscribe&lt;T&gt;(handler) 集中注册所有事件。
        /// </summary>
        protected abstract void Bind();

        /// <summary>
        /// 订阅事件并自动登记反注册闭包。
        /// 调用方只需要在 Bind() 中调用一次，无需在 OnDisable 中手动取消订阅。
        /// </summary>
        protected void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            EventBus.Subscribe(handler);
            // 闭包捕获 handler，确保 Unsubscribe 时传入的是同一个引用
            _unsubscribers.Add(() => EventBus.Unsubscribe(handler));
        }

        private void Unbind()
        {
            for (int i = 0; i < _unsubscribers.Count; i++) _unsubscribers[i]();
            _unsubscribers.Clear();
        }
    }
}
