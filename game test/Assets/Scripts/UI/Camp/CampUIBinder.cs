using UnityEngine;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 绑定器：
    /// 仅负责保存 UI 引用，具体逻辑由 CampUIView 处理。
    /// </summary>
    public class CampUIBinder : MonoBehaviour
    {
        [Header("Layout")]
        // 菜单根节点（可选，用于整体定位/动画）
        [SerializeField] private RectTransform rootRect;
        // 垂直布局容器（用于动态摆放按钮）
        [SerializeField] private Transform menuContainer;
        // 按钮预制体（必须带 CampActionButton 组件）
        [SerializeField] private CampActionButton buttonPrefab;
        // 透明度与交互控制
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary> 菜单根节点 </summary>
        public RectTransform RootRect => rootRect;
        /// <summary> 按钮容器 </summary>
        public Transform MenuContainer => menuContainer;
        /// <summary> 按钮预制体 </summary>
        public CampActionButton ButtonPrefab => buttonPrefab;
        /// <summary> CanvasGroup 控制器 </summary>
        public CanvasGroup CanvasGroup => canvasGroup;
    }
}
