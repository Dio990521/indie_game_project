using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.SkillTree
{
    /// <summary>
    /// 技能树 UI 绑定器（Binder）：
    /// 严格只做引用容器，不写任何业务逻辑。
    /// </summary>
    public class SkillTreeBinder : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Header")]
        [Tooltip("显示当前 SP 数值的文本，格式 技能点: N")]
        [SerializeField] private TMP_Text spValueText;
        [Tooltip("关闭按钮")]
        [SerializeField] private Button closeButton;

        [Header("Category Tabs")]
        [Tooltip("分类 Tab 按钮数组, 顺序: Combat=0, Exploration=1, Crafting=2")]
        [SerializeField] private Button[] categoryTabButtons;
        [Tooltip("Tab 选中高亮数组，与 categoryTabButtons 一一对应")]
        [SerializeField] private GameObject[] categoryTabHighlights;

        [Header("Skill Grid")]
        [Tooltip("技能节点的父容器(推荐挂 GridLayoutGroup)")]
        [SerializeField] private Transform skillGridRoot;
        [Tooltip("单个技能节点预制体（带 SkillNodeUI 组件）")]
        [SerializeField] private GameObject skillNodePrefab;
        [Tooltip("空列表提示节点（即将开放文字），无技能时显示")]
        [SerializeField] private GameObject emptyStateNode;

        // --- 只读属性 ---
        public CanvasGroup CanvasGroup             => canvasGroup;
        public TMP_Text SpValueText                => spValueText;
        public Button CloseButton                  => closeButton;
        public Button[] CategoryTabButtons         => categoryTabButtons;
        public GameObject[] CategoryTabHighlights  => categoryTabHighlights;
        public Transform SkillGridRoot             => skillGridRoot;
        public GameObject SkillNodePrefab          => skillNodePrefab;
        public GameObject EmptyStateNode           => emptyStateNode;
    }
}
