using UnityEngine;

namespace IndieGame.UI.Town
{
    /// <summary>
    /// 城镇 UI 绑定器：
    /// 仅负责持有 UI 组件引用，具体显示逻辑由 TownUIView 处理。
    /// </summary>
    public class TownUIBinder : MonoBehaviour
    {
        [Header("Layout")]
        // 菜单根节点（用于整体定位/动画）
        [SerializeField] private RectTransform rootRect;
        // 按钮容器（垂直或网格布局）
        [SerializeField] private Transform buttonContainer;
        // 按钮预制体（必须带 TownActionButton 组件）
        [SerializeField] private TownActionButton buttonPrefab;
        // 透明度与交互控制
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary>菜单根节点</summary>
        public RectTransform RootRect => rootRect;
        /// <summary>按钮容器</summary>
        public Transform ButtonContainer => buttonContainer;
        /// <summary>按钮预制体</summary>
        public TownActionButton ButtonPrefab => buttonPrefab;
        /// <summary>CanvasGroup 控制器</summary>
        public CanvasGroup CanvasGroup => canvasGroup;
    }
}
