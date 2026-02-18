using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.TitleScreen
{
    /// <summary>
    /// 标题读档菜单绑定器（Binder）：
    /// 仅负责在 Inspector 中收集 UI 引用并提供只读访问器。
    ///
    /// 约束：
    /// - Binder 不写业务逻辑
    /// - 不处理事件订阅
    /// - 不做数据计算
    /// </summary>
    public class SaveLoadMenuBinder : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Transform listContainer;
        [SerializeField] private Button slotButtonPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject emptyStateNode;

        [Header("Optional")]
        [Tooltip("可选：用于显示无存档时的说明文本（如果你用 emptyStateNode 内自己的文本可不绑定）")]
        [SerializeField] private TMP_Text emptyStateText;

        public GameObject RootPanel => rootPanel;
        public Transform ListContainer => listContainer;
        public Button SlotButtonPrefab => slotButtonPrefab;
        public Button CloseButton => closeButton;
        public GameObject EmptyStateNode => emptyStateNode;
        public TMP_Text EmptyStateText => emptyStateText;
    }
}
