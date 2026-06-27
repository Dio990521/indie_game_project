using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        [Header("Tabs")]
        [Tooltip("原型制造 Tab 按钮")]
        [SerializeField] private Button prototypeTabButton;
        [Tooltip("复现制造 Tab 按钮")]
        [SerializeField] private Button replicationTabButton;
        [Tooltip("强化 Tab 按钮")]
        [SerializeField] private Button enhanceTabButton;

        [Header("Requirement Item (Optional)")]
        [Tooltip("材料条目预制体（可选但推荐配置）。若未配置，右侧将无法生成材料列表。")]
        [SerializeField] private GameObject requirementSlotPrefab;

        [Header("强化 Tab 内容根节点")]
        [Tooltip("Prototype/Replication 共用的内容根节点（左侧图纸列表+右侧需求详情），切到强化 Tab 时隐藏")]
        [SerializeField] private GameObject standardTabContentRoot;
        [Tooltip("整个强化Tab的内容根节点（与 Prototype/Replication 的内容区是不同布局，切 Tab 时整体显隐）")]
        [SerializeField] private GameObject enhanceRootNode;

        [Header("强化 - 左侧武器列表")]
        [SerializeField] private Transform weaponListRoot;
        [SerializeField] private GameObject weaponSlotPrefab;

        [Header("强化 - 右侧语料库列表")]
        [SerializeField] private Transform prefixListRoot;
        [SerializeField] private GameObject prefixSlotPrefab;

        [Header("强化 - 武器基础信息")]
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text weaponKindText;
        [SerializeField] private TMP_Text baseHPText;
        [SerializeField] private TMP_Text baseAttackText;
        [SerializeField] private TMP_Text baseDefenseText;
        [SerializeField] private TMP_Text baseChargeRateText;
        [SerializeField] private GameObject enhanceEmptyStateNode;
        [SerializeField] private GameObject enhanceDetailNode;

        [Header("强化 - 已应用前缀列表")]
        [SerializeField] private Transform appliedPrefixListRoot;
        [SerializeField] private GameObject appliedPrefixSlotPrefab;

        [Header("强化 - 操作按钮")]
        [SerializeField] private Button enhanceConfirmButton;
        [SerializeField] private TMP_Text enhanceCostText;
        [SerializeField] private Button rebindConfirmButton;
        [SerializeField] private TMP_Text rebindCostText;
        [SerializeField] private Button renameButton;

        public Image ProductIcon => productIcon;
        public Transform RequirementsRoot => requirementsRoot;
        public Button CraftButton => craftButton;
        public GameObject EmptyStateNode => emptyStateNode;

        public Transform ListRoot => listRoot;
        public GameObject SlotPrefab => slotPrefab;
        public Button PrototypeTabButton => prototypeTabButton;
        public Button ReplicationTabButton => replicationTabButton;
        public Button EnhanceTabButton => enhanceTabButton;

        public GameObject RequirementSlotPrefab => requirementSlotPrefab;

        public GameObject StandardTabContentRoot => standardTabContentRoot;
        public GameObject EnhanceRootNode => enhanceRootNode;

        public Transform WeaponListRoot => weaponListRoot;
        public GameObject WeaponSlotPrefab => weaponSlotPrefab;

        public Transform PrefixListRoot => prefixListRoot;
        public GameObject PrefixSlotPrefab => prefixSlotPrefab;

        public TMP_Text WeaponNameText => weaponNameText;
        public TMP_Text WeaponKindText => weaponKindText;
        public TMP_Text BaseHPText => baseHPText;
        public TMP_Text BaseAttackText => baseAttackText;
        public TMP_Text BaseDefenseText => baseDefenseText;
        public TMP_Text BaseChargeRateText => baseChargeRateText;
        public GameObject EnhanceEmptyStateNode => enhanceEmptyStateNode;
        public GameObject EnhanceDetailNode => enhanceDetailNode;

        public Transform AppliedPrefixListRoot => appliedPrefixListRoot;
        public GameObject AppliedPrefixSlotPrefab => appliedPrefixSlotPrefab;

        public Button EnhanceConfirmButton => enhanceConfirmButton;
        public TMP_Text EnhanceCostText => enhanceCostText;
        public Button RebindConfirmButton => rebindConfirmButton;
        public TMP_Text RebindCostText => rebindCostText;
        public Button RenameButton => renameButton;
    }
}
