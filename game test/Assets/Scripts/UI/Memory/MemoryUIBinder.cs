using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Memory
{
    /// <summary>
    /// Memory 图鉴 UI 绑定器：
    /// 纯引用容器，不含任何业务逻辑。
    /// 所有 UI 组件引用均通过 Inspector 配置，由 MemoryUIController 驱动。
    /// </summary>
    public class MemoryUIBinder : MonoBehaviour
    {
        [Header("根节点")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Tab 按钮（顺序必须对应 MemoryTab 枚举 0-5：图纸/武器/道具/素材/语料/任务）")]
        [SerializeField] private Button[] tabButtons;
        // Tab 选中状态高亮节点（如下划线或背景色块）
        [SerializeField] private GameObject[] tabHighlights;

        [Header("列表区")]
        [Tooltip("MemorySlotUI 预制体")]
        [SerializeField] private GameObject slotPrefab;
        [Tooltip("ScrollRect 的 Content 根节点")]
        [SerializeField] private Transform listRoot;

        [Header("详情面板")]
        // 无选中时整体隐藏此节点
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailNameText;
        // 副标签：显示分类/状态等附加信息
        [SerializeField] private TMP_Text detailSubtitleText;
        [SerializeField] private TMP_Text detailDescText;

        [Header("空状态")]
        [Tooltip("当前 Tab 无数据时显示")]
        [SerializeField] private GameObject emptyStateNode;
        [SerializeField] private TMP_Text emptyStateText;

        [Header("任务 Tab 占位面板")]
        [Tooltip("切换到任务 Tab 时显示，其余 Tab 时隐藏")]
        [SerializeField] private GameObject taskPlaceholderPanel;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        // ── 只读属性 ─────────────────────────────────────────────────────

        public CanvasGroup CanvasGroup         => canvasGroup;
        public Button[] TabButtons             => tabButtons;
        public GameObject[] TabHighlights      => tabHighlights;
        public GameObject SlotPrefab           => slotPrefab;
        public Transform ListRoot              => listRoot;
        public GameObject DetailPanel          => detailPanel;
        public Image DetailIcon                => detailIcon;
        public TMP_Text DetailNameText         => detailNameText;
        public TMP_Text DetailSubtitleText     => detailSubtitleText;
        public TMP_Text DetailDescText         => detailDescText;
        public GameObject EmptyStateNode       => emptyStateNode;
        public TMP_Text EmptyStateText         => emptyStateText;
        public GameObject TaskPlaceholderPanel => taskPlaceholderPanel;
        public Button CloseButton              => closeButton;
    }
}
