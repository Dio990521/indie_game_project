using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 打造界面绑定器（仅引用，不写业务逻辑）：
    /// 负责把 Inspector 中的 UI 组件引用集中暴露给 Controller。
    ///
    /// 重要约束：
    /// - Binder 只做“字段容器”，不参与数据计算与事件监听。
    /// - Controller 通过它拿到引用后进行统一刷新。
    /// </summary>
    public class CraftUIBinder : MonoBehaviour
    {
        [Header("Right Panel")]
        [SerializeField] private Image productIcon;
        [SerializeField] private Transform requirementsRoot;
        [SerializeField] private Button craftButton;
        [SerializeField] private GameObject emptyStateNode;

        [Header("Left Panel")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private GameObject slotPrefab;

        [Header("Requirement Item (Optional)")]
        [Tooltip("材料条目预制体（可选但推荐配置）。若未配置，右侧将无法生成材料列表。")]
        [SerializeField] private GameObject requirementSlotPrefab;

        public Image ProductIcon => productIcon;
        public Transform RequirementsRoot => requirementsRoot;
        public Button CraftButton => craftButton;
        public GameObject EmptyStateNode => emptyStateNode;

        public Transform ListRoot => listRoot;
        public GameObject SlotPrefab => slotPrefab;

        public GameObject RequirementSlotPrefab => requirementSlotPrefab;
    }
}
