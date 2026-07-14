using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 调试日志工具：封装 Unity Debug。
    /// - Log / LogWarning：仅在 Editor 与 Development Build 输出，正式包剥离；
    /// - LogError（M9 修复）：所有构建都输出。错误日志是玩家反馈"存档丢了/卡死了"时
    ///   唯一的定位线索（Unity 会写入 Player.log），且 EventBus 的异常隔离依赖它落盘，
    ///   正式包剥离等于把所有故障变成静默故障。
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
            // M9：错误日志在所有构建中保留（写入 Player.log，供问题定位）
            Debug.LogError(message);
        }

        public static void LogError(object message, Object context)
        {
            // M9：错误日志在所有构建中保留（写入 Player.log，供问题定位）
            Debug.LogError(message, context);
        }
    }
}
