using UnityEngine;
using UnityEngine.UI;

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

        [Header("背景图")]
        // 城镇背景图（各城镇可在对应 TownTile SO 中单独配置 Sprite）
        [SerializeField] private Image bgImage;

        /// <summary>菜单根节点</summary>
        public RectTransform RootRect => rootRect;
        /// <summary>按钮容器</summary>
        public Transform ButtonContainer => buttonContainer;
        /// <summary>按钮预制体</summary>
        public TownActionButton ButtonPrefab => buttonPrefab;
        /// <summary>CanvasGroup 控制器</summary>
        public CanvasGroup CanvasGroup => canvasGroup;
        /// <summary>城镇背景图组件</summary>
        public Image BgImage => bgImage;
    }
}
