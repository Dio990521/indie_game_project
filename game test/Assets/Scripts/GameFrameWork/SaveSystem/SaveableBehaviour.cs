using UnityEngine;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 可参与存档的非单例 MonoBehaviour 基类：
    /// 提供与 SaveableMonoSingleton 相同的注册/注销机制，但不绑定单例模式，
    /// 用于挂载在普通对象（如玩家身上）的存档模块。
    ///
    /// 关键设计：
    /// - <see cref="UnregisterOnDisable"/> 默认 true，但允许子类 override 为 false。
    ///   场景需求示例：玩家在 Camp 场景会被临时 SetActive(false)，
    ///   此时若注销则自动存档时无法采集玩家数据；
    ///   该类子类只需把 UnregisterOnDisable 设为 false，OnDestroy 会兜底注销。
    /// </summary>
    public abstract class SaveableBehaviour : MonoBehaviour, ISaveable
    {
        protected SaveManager _saveManager;
        protected bool _isRegisteredToSaveManager;

        /// <summary> 存档模块唯一标识。 </summary>
        public abstract string SaveID { get; }

        /// <summary> 捕获当前模块状态。 </summary>
        public abstract object CaptureState();

        /// <summary> 从存档数据恢复状态。 </summary>
        public abstract void RestoreState(object data);

        /// <summary>
        /// 是否在 OnDisable 时注销到 SaveManager（默认 true）。
        /// 若对象生命周期内会出现“仅暂时禁用”的场景，请 override 为 false，
        /// 并依赖 OnDestroy 真正注销。
        /// </summary>
        protected virtual bool UnregisterOnDisable => true;

        /// <summary>
        /// 启用时自动尝试注册到 SaveManager。
        /// 子类如需追加逻辑，请 override 并调用 base.OnEnable()。
        /// </summary>
        protected virtual void OnEnable()
        {
            EnsureSaveRegistration(forceSearch: true);
        }

        /// <summary>
        /// 禁用时根据 UnregisterOnDisable 决定是否注销。
        /// </summary>
        protected virtual void OnDisable()
        {
            if (!UnregisterOnDisable) return;
            UnregisterFromSaveManager();
        }

        /// <summary>
        /// 真正销毁时强制注销，防止脏引用残留。
        /// </summary>
        protected virtual void OnDestroy()
        {
            UnregisterFromSaveManager();
        }

        /// <summary>
        /// 确保已向 SaveManager 注册（幂等）。
        /// </summary>
        protected void EnsureSaveRegistration(bool forceSearch)
        {
            if (_isRegisteredToSaveManager) return;

            _saveManager = ResolveSaveManager(forceSearch);
            if (_saveManager == null) return;

            _saveManager.Register(this);
            _isRegisteredToSaveManager = true;
        }

        /// <summary>
        /// 注销当前模块到 SaveManager（幂等）。
        /// </summary>
        protected void UnregisterFromSaveManager()
        {
            if (_isRegisteredToSaveManager && _saveManager != null)
            {
                _saveManager.Unregister(this);
            }
            _isRegisteredToSaveManager = false;
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
