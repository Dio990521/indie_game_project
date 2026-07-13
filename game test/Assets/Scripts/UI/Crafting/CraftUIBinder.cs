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

        [Header("合成大类子分类（配方=未打造、道具=已打造；与装备的4个按钮同放在 SubFilter 下，靠 Controller 按大类逐个控制显隐）")]
        [Tooltip("合成·配方（未打造）")]
        [SerializeField] private Button blueprintListModeButton;
        [Tooltip("合成·道具（已打造）")]
        [SerializeField] private Button craftedListModeButton;

        [Header("装备大类子分类（武器图纸/防具图纸/武器/防具；与合成的2个按钮同放在 SubFilter 下，靠 Controller 按大类逐个控制显隐）")]
        [Tooltip("未打造 + 武器")]
        [SerializeField] private Button weaponBlueprintTabButton;
        [Tooltip("未打造 + 防具")]
        [SerializeField] private Button armorBlueprintTabButton;
        [Tooltip("已打造 + 武器")]
        [SerializeField] private Button weaponCraftedTabButton;
        [Tooltip("已打造 + 防具")]
        [SerializeField] private Button armorCraftedTabButton;

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

        public Button WeaponBlueprintTabButton => weaponBlueprintTabButton;
        public Button ArmorBlueprintTabButton => armorBlueprintTabButton;
        public Button WeaponCraftedTabButton => weaponCraftedTabButton;
        public Button ArmorCraftedTabButton => armorCraftedTabButton;

        public GameObject RequirementSlotPrefab => requirementSlotPrefab;

        public Transform CraftEffectsRoot => craftEffectsRoot;
        public GameObject CraftEffectSlotPrefab => craftEffectSlotPrefab;
    }
}
