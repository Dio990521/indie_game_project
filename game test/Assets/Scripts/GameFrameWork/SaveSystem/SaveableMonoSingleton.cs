using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 可参与存档的单例基类：
    /// 在 MonoSingleton 基础上自动接入 SaveManager 注册/注销，
    /// 解决以往各系统重复编写 EnsureSaveRegistration / ResolveSaveManager 的问题。
    ///
    /// 使用方法：
    /// <para>public class MyManager : SaveableMonoSingleton&lt;MyManager&gt;</para>
    ///
    /// 子类约定：
    /// 1) 必须实现 SaveID / CaptureState / RestoreState；
    /// 2) 若需要在 Awake 内做自定义初始化，需 override Awake 并调用 base.Awake()；
    /// 3) 若需要自定义 OnEnable / OnDisable，需 override 并调用 base.OnEnable() / base.OnDisable()
    ///    以保留注册/注销行为。
    /// </summary>
    /// <typeparam name="T">具体子类类型。</typeparam>
    public abstract class SaveableMonoSingleton<T> : MonoSingleton<T>, ISaveable
        where T : MonoBehaviour
    {
        // 缓存 SaveManager 引用，避免每次 OnEnable 都查找场景
        protected SaveManager _saveManager;
        // 已成功向 SaveManager 注册的标记，用于幂等控制
        protected bool _isRegisteredToSaveManager;

        /// <summary> 存档模块唯一标识。 </summary>
        public abstract string SaveID { get; }

        /// <summary> 捕获当前模块状态。 </summary>
        public abstract object CaptureState();

        /// <summary> 从存档数据恢复状态。 </summary>
        public abstract void RestoreState(object data);

        /// <summary>
        /// 启用时自动尝试注册到 SaveManager。
        /// 子类如需追加逻辑，请 override 并调用 base.OnEnable()。
        /// </summary>
        protected virtual void OnEnable()
        {
            EnsureSaveRegistration(forceSearch: false);
        }

        /// <summary>
        /// 禁用时自动注销注册，避免 SaveManager 持有失效引用。
        /// 子类如需保留注册（如玩家对象临时隐藏），请 override 并改写策略。
        /// </summary>
        protected virtual void OnDisable()
        {
            if (_isRegisteredToSaveManager && _saveManager != null)
            {
                _saveManager.Unregister(this);
            }
            _isRegisteredToSaveManager = false;
        }

        /// <summary>
        /// 确保已向 SaveManager 注册（幂等）。
        /// </summary>
        /// <param name="forceSearch">true 表示即使尚未缓存也强制查找 SaveManager。</param>
        protected void EnsureSaveRegistration(bool forceSearch)
        {
            if (_isRegisteredToSaveManager) return;

            _saveManager = ResolveSaveManager(forceSearch);
            if (_saveManager == null) return;

            _saveManager.Register(this);
            _isRegisteredToSaveManager = true;
        }

        /// <summary>
        /// 查找 SaveManager：
        /// 使用场景查找而非 SaveManager.Instance，避免在缺失时产生无意义警告日志。
        /// </summary>
        private SaveManager ResolveSaveManager(bool forceSearch)
        {
            if (_saveManager != null) return _saveManager;
            if (!forceSearch && _isRegisteredToSaveManager) return null;
            return FindAnyObjectByType<SaveManager>();
        }
    }
}
