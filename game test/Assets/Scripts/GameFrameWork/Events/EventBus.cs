using System;
using System.Collections.Generic;

namespace IndieGame.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new Dictionary<Type, Delegate>();

        // Lightweight global event hub for decoupled systems.
        public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Type type = typeof(T);
            if (Handlers.TryGetValue(type, out Delegate existing))
            {
                Handlers[type] = (Action<T>)existing + handler;
                return;
            }
            Handlers[type] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out Delegate existing)) return;
            Action<T> updated = (Action<T>)existing - handler;
            if (updated == null)
            {
                Handlers.Remove(type);
                return;
            }
            Handlers[type] = updated;
        }

        public static void Raise<T>(T evt)
        {
            Type type = typeof(T);
            if (!Handlers.TryGetValue(type, out Delegate existing)) return;
            ((Action<T>)existing)?.Invoke(evt);
        }

        public static bool HasSubscribers<T>()
        {
            Type type = typeof(T);
            return Handlers.TryGetValue(type, out Delegate existing) && existing != null;
        }
    }
}
