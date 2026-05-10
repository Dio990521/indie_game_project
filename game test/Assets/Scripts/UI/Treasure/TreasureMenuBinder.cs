using UnityEngine;

namespace IndieGame.UI.Treasure
{
    /// <summary>
    /// 宝具菜单 UI 引用绑定器：集中保存所有子控件引用，仿 BoardActionMenuBinder 模式。
    /// 挂载在 TreasureMenuView 的根 GameObject 上，TreasureMenuView 通过此组件访问子控件。
    /// </summary>
    public class TreasureMenuBinder : MonoBehaviour
    {
        [Tooltip("菜单根节点的 RectTransform（用于位置控制和动画）")]
        [SerializeField] private RectTransform rootRect;

        [Tooltip("控制整体显示/隐藏的 CanvasGroup（alpha、raycast 开关）")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("宝具列表容器（挂 Vertical Layout Group），Slot 实例化到这里")]
        [SerializeField] private Transform slotContainer;

        [Tooltip("单行宝具条目的预制体，需挂载 TreasureSlotUI 脚本")]
        [SerializeField] private TreasureSlotUI slotPrefab;

        public RectTransform RootRect => rootRect;
        public CanvasGroup CanvasGroup => canvasGroup;
        public Transform SlotContainer => slotContainer;
        public TreasureSlotUI SlotPrefab => slotPrefab;
    }
}
