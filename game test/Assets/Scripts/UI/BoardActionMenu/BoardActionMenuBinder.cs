using UnityEngine;

namespace IndieGame.UI
{
    /// <summary>
    /// 棋盘操作菜单绑定器：
    /// 用于集中保存 UI 引用，降低 View 脚本中的查找成本。
    /// </summary>
    public class BoardActionMenuBinder : MonoBehaviour
    {
        // 根 Rect（用于定位菜单整体位置）
        [SerializeField] private RectTransform rootRect;
        // 按钮容器（用于挂载按钮实例）
        [SerializeField] private Transform buttonContainer;
        // 按钮预制体（用于动态创建按钮）
        [SerializeField] private BoardActionButton buttonPrefab;
        // 透明度与交互控制
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary> 菜单根节点 </summary>
        public RectTransform RootRect => rootRect;
        /// <summary> 按钮容器 </summary>
        public Transform ButtonContainer => buttonContainer;
        /// <summary> 按钮预制体 </summary>
        public BoardActionButton ButtonPrefab => buttonPrefab;
        /// <summary> CanvasGroup 控制器 </summary>
        public CanvasGroup CanvasGroup => canvasGroup;
    }
}
