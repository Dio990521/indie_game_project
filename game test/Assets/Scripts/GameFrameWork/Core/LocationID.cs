using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace IndieGame.Core
{
    /// <summary>
    /// 位置标识（ScriptableObject）：
    /// 用于跨场景定位出生点、传送点等。
    /// 通过资源实例作为“稳定的唯一 ID”使用。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Scene/Location ID")]
    public class LocationID : ScriptableObject
    {
        [SerializeField]
        [FormerlySerializedAs("displayName")]
        // 显示名称（支持本地化）
        private LocalizedString displayName;
        /// <summary> 对外只读显示名称 </summary>
        public LocalizedString DisplayName => displayName;
    }
}
