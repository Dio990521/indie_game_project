using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 调试日志工具：封装 Unity Debug，仅在 Editor 下输出日志，打包后方法体为空。
    /// </summary>
    public static class DebugTools
    {
        public static void Log(object message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(message);
#endif
        }

        public static void Log(object message, Object context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(message, context);
#endif
        }

        public static void LogWarning(object message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(message);
#endif
        }

        public static void LogWarning(object message, Object context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(message, context);
#endif
        }

        public static void LogError(object message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(message);
#endif
        }

        public static void LogError(object message, Object context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(message, context);
#endif
        }
    }
}
