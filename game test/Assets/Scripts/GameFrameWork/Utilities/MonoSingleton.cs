using UnityEngine;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 泛型单例模式基类 (继承自 MonoBehaviour)
    /// <para>使用方法: public class MyManager : MonoSingleton<MyManager></para>
    /// </summary>
    /// <typeparam name="T">继承此类的具体类型</typeparam>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning($"[MonoSingleton] Instance '{typeof(T)}' already destroyed on application quit." +
                                     " Won't create again - returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindAnyObjectByType<T>();

                        if (FindObjectsByType<T>(FindObjectsSortMode.None).Length > 1)
                        {
                            Debug.LogError($"[MonoSingleton] Something went really wrong " +
                                           $" - there should never be more than 1 singleton! Reopening the scene might fix it.");
                            return _instance;
                        }

                        if (_instance == null)
                        {
                            Debug.LogWarning($"[MonoSingleton] Instance of {typeof(T)} not found. Ensure GameBootstrapper created it.");
                        }
                    }

                    return _instance;
                }
            }
        }

        /// <summary>
        /// 是否在加载新场景时销毁？默认 false (即由于是单例，通常希望它存活)
        /// 子类可以重写此属性返回 true 来改变行为
        /// </summary>
        protected virtual bool DestroyOnLoad => false;

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (DestroyOnLoad && transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                Debug.Log($"[MonoSingleton] Deleting extra instance of {typeof(T)} attached to {gameObject.name}");
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
