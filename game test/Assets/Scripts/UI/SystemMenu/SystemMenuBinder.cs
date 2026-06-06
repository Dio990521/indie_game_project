using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.SystemMenu
{
    /// <summary>
    /// 系统菜单绑定器（Binder）：
    /// 严格遵循项目 Binder 约束，只负责在 Inspector 中收集引用并对外提供只读访问。
    ///
    /// 约束说明：
    /// 1) 不写任何业务逻辑；
    /// 2) 不订阅事件；
    /// 3) 不做数值计算；
    /// 4) 仅作为 View/Controller 的"引用容器"。
    /// </summary>
    public class SystemMenuBinder : MonoBehaviour
    {
        [Header("Toggle Button")]
        [SerializeField] private Button systemButton;
        // 系统按钮自身的 CanvasGroup，用于对话/Loading 期间淡出隐藏
        [SerializeField] private CanvasGroup buttonCanvasGroup;

        [Header("Panel")]
        [SerializeField] private CanvasGroup panelCanvasGroup;
        // 可选：用于弹出时的缩放动画
        [SerializeField] private RectTransform panelRect;

        [Header("Backdrop")]
        // 透明全屏遮罩，点击时关闭面板；平时 SetActive(false) 不参与命中检测
        [SerializeField] private Button backdropButton;

        [Header("Language Buttons")]
        [SerializeField] private Button btnZhHans;
        [SerializeField] private Button btnZhHant;
        [SerializeField] private Button btnEn;
        [SerializeField] private Button btnJa;

        [Header("Action Buttons")]
        // 存档按钮
        [SerializeField] private Button btnSave;
        // 读档按钮
        [SerializeField] private Button btnLoad;
        // 返回标题按钮
        [SerializeField] private Button btnReturnToTitle;

        [Header("Highlight Colors")]
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color normalColor   = new Color(0.55f, 0.55f, 0.55f, 1f);

        // ── 只读属性 ─────────────────────────────────────────────────────────
        public Button        SystemButton        => systemButton;
        public CanvasGroup   ButtonCanvasGroup   => buttonCanvasGroup;
        public CanvasGroup   PanelCanvasGroup    => panelCanvasGroup;
        public RectTransform PanelRect           => panelRect;
        public Button        BackdropButton      => backdropButton;
        public Button        BtnZhHans           => btnZhHans;
        public Button        BtnZhHant           => btnZhHant;
        public Button        BtnEn               => btnEn;
        public Button        BtnJa               => btnJa;
        public Button        BtnSave             => btnSave;
        public Button        BtnLoad             => btnLoad;
        public Button        BtnReturnToTitle    => btnReturnToTitle;
        public Color         SelectedColor       => selectedColor;
        public Color         NormalColor         => normalColor;
    }
}
