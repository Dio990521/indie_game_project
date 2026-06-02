using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 泛型单例模式基类（继承自 MonoBehaviour）。
    /// <para>使用方法：public class MyManager : MonoSingleton&lt;MyManager&gt;</para>
    /// <para>
    /// 历史问题说明：
    /// 旧版本曾使用 <c>DestroyOnLoad</c> 属性，但命名与实际行为完全反向
    /// （DestroyOnLoad=true 时反而调用 DontDestroyOnLoad 保留对象）。
    /// 现统一改为 <see cref="KeepAcrossScenes"/>，语义和命名一致：
    /// 返回 true 表示"希望跨场景保留"，对应调用 DontDestroyOnLoad。
    /// </para>
    /// </summary>
    /// <typeparam name="T">继承此类的具体类型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        /// <summary>
        /// 轻量检测：实例是否存在。不触发 Find 也不打 Warning，专门用于 OnDestroy 等销毁期的安全判断。
        /// </summary>
        public static bool HasInstance => _instance != null && !_applicationIsQuitting;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindAnyObjectByType<T>();

                        if (FindObjectsByType<T>(FindObjectsSortMode.None).Length > 1)
                        {
                            DebugTools.LogError($"[MonoSingleton] Something went really wrong " +
                                           $" - there should never be more than 1 singleton! Reopening the scene might fix it.");
                            return _instance;
                        }

                        if (_instance == null)
                        {
                            DebugTools.LogWarning($"[MonoSingleton] Instance of {typeof(T)} not found. Ensure GameBootstrapper created it.");
                        }
                    }

                    return _instance;
                }
            }
        }

        /// <summary>
        /// 是否需要跨场景保留该单例？
        /// <para>默认 false：随场景一起销毁（即 Unity 默认行为）。</para>
        /// <para>子类重写返回 true 时：Awake 阶段会调用 <c>DontDestroyOnLoad</c>，使其在场景切换时存活。</para>
        /// <para>
        /// 命名/语义说明：旧 <c>DestroyOnLoad</c> 属性命名与代码语义反向，
        /// 已弃用。新代码请使用本属性。
        /// </para>
        /// </summary>
        protected virtual bool KeepAcrossScenes => false;

        // 注：历史上的 DestroyOnLoad 属性已移除（命名与实际行为反向，且本项目所有 Manager
        // 都挂在 GameBootstrapper 的 [GameSystem] 常驻根节点下，跨场景保留由父节点 DontDestroyRoot
        // 提供，基类的 DontDestroyOnLoad 调用从未实际触发）。
        // 子类如需控制单例自身是否跨场景保留，请重写 KeepAcrossScenes。

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                // 仅在根对象（无父级）上调用 DontDestroyOnLoad，避免 Unity 因父子层级丢失而打 Warning。
                if (KeepAcrossScenes && transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                DebugTools.Log($"[MonoSingleton] Deleting extra instance of {typeof(T)} attached to {gameObject.name}");
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
