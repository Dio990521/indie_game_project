using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Exploration
{
    /// <summary>
    /// 出生点组件：
    /// 在探索场景（如城镇、迷宫）中标记一个具体的位置。
    /// 配合 LocationID 使用，使得场景加载系统（SceneLoader）能够跨场景精确找到玩家应该出现的位置。
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField]
        [Tooltip("该出生点的唯一身份标识。通常是一个 ScriptableObject 资源。")]
        private LocationID locationId;

        /// <summary>
        /// 对外暴露的只读 LocationID，供注册表与外部系统查询。
        /// </summary>
        public LocationID LocationId => locationId;

        /// <summary>
        /// 当对象启用时（或进入场景时）执行。
        /// 将自己注册到全局字典中。
        /// </summary>
        private void OnEnable()
        {
            if (locationId == null)
            {
                Debug.LogWarning("[SpawnPoint] 缺失 LocationID，该出生点将无法被寻址。");
                return;
            }

            // 交由 SpawnPointRegistry 统一管理注册逻辑
            SpawnPointRegistry.Register(this);
        }

        /// <summary>
        /// 当对象禁用或销毁时执行。
        /// 负责从注册表中移除自己，防止内存泄漏或返回已销毁对象的引用。
        /// </summary>
        private void OnDisable()
        {
            if (locationId == null) return;

            // 交由 SpawnPointRegistry 统一管理注销逻辑
            SpawnPointRegistry.Unregister(this);
        }
    }
}
