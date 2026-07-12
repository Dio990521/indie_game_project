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
        [SerializeField] private TMP_Text blueprintLevelText;
        [SerializeField] private TMP_Text blueprintDescriptionText;
        [SerializeField] private Transform requirementsRoot;
        [SerializeField] private Button craftButton;
        [SerializeField] private GameObject emptyStateNode;

        [Header("Left Panel")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private GameObject slotPrefab;

        [Header("大类 Tab（装备/合成）")]
        [SerializeField] private Button equipmentCategoryButton;
        [SerializeField] private Button synthesisCategoryButton;

        [Header("列表模式（未打造/已打造）")]
        [SerializeField] private Button blueprintListModeButton;
        [SerializeField] private Button craftedListModeButton;

        [Header("装备部位筛选（仅装备大类下显示）")]
        [SerializeField] private GameObject equipmentSubFilterRoot;
        [SerializeField] private Button weaponFilterButton;
        [SerializeField] private Button armorFilterButton;
        [SerializeField] private Button allFilterButton;

        [Header("Requirement Item (Optional)")]
        [Tooltip("材料条目预制体（可选但推荐配置）。若未配置，右侧将无法生成材料列表。")]
        [SerializeField] private GameObject requirementSlotPrefab;

        [Header("打造效果预览")]
        [SerializeField] private Transform craftEffectsRoot;
        [SerializeField] private GameObject craftEffectSlotPrefab;

        public Image ProductIcon => productIcon;
        public TMP_Text BlueprintLevelText => blueprintLevelText;
        public TMP_Text BlueprintDescriptionText => blueprintDescriptionText;
        public Transform RequirementsRoot => requirementsRoot;
        public Button CraftButton => craftButton;
        public GameObject EmptyStateNode => emptyStateNode;

        public Transform ListRoot => listRoot;
        public GameObject SlotPrefab => slotPrefab;

        public Button EquipmentCategoryButton => equipmentCategoryButton;
        public Button SynthesisCategoryButton => synthesisCategoryButton;

        public Button BlueprintListModeButton => blueprintListModeButton;
        public Button CraftedListModeButton => craftedListModeButton;

        public GameObject EquipmentSubFilterRoot => equipmentSubFilterRoot;
        public Button WeaponFilterButton => weaponFilterButton;
        public Button ArmorFilterButton => armorFilterButton;
        public Button AllFilterButton => allFilterButton;

        public GameObject RequirementSlotPrefab => requirementSlotPrefab;

        public Transform CraftEffectsRoot => craftEffectsRoot;
        public GameObject CraftEffectSlotPrefab => craftEffectSlotPrefab;
    }
}
