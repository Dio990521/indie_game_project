using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IndieGame.UI.Inventory;

namespace IndieGame.UI.Equipment
{
    /// <summary>
    /// 装备界面的 UI 引用容器。
    /// 严格遵守 Binder 规范：仅暴露引用，不写任何逻辑。
    /// 物品格复用背包的 InventorySlotUI 预制体（图标/稀有度背景/数量/选中高亮），不重复造轮子。
    /// </summary>
    public class EquipmentUIBinder : MonoBehaviour
    {
        [Header("根节点")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("分类 Tab（顺序必须与 EquipmentType 枚举一致：Weapon(武器) / Armor(防具) / Recipe(配方)）")]
        [SerializeField] private Button[] categoryTabButtons;
        // 每个 Tab 对应的高亮 GameObject（选中时激活）
        [SerializeField] private GameObject[] categoryTabHighlights;

        [Header("物品格（复用背包 InventorySlotUI）")]
        [SerializeField] private Transform itemGridRoot;
        [SerializeField] private InventorySlotUI slotPrefab;
        // 物品网格所在的 ScrollRect（可选）：切换 Tab 时用于把滚动位置重置回顶部
        [SerializeField] private ScrollRect gridScrollRect;

        [Header("详情面板")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailNameText;
        // 装备类型文本（武器/防具/配方）
        [SerializeField] private TMP_Text detailTypeText;
        [SerializeField] private TMP_Text detailDescText;
        // 稀有度色块：背景色 + 文字（普通/优良/稀有/史诗/传说）
        [SerializeField] private Image detailRarityBadgeBackground;
        [SerializeField] private TMP_Text detailRarityBadgeText;
        // 属性加成列表容器：运行时按 Modifiers 数量动态生成 modifierRowPrefab 的实例
        [SerializeField] private Transform detailModifiersRoot;
        [SerializeField] private TMP_Text modifierRowPrefab;
        // 装备/卸下按钮：同一个按钮，文案随选中项的装备状态切换
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipButtonLabel;

        [Header("当前已装备槽位（武器 / 防具 / 配方x2，配方系统未实现，恒为空槽）")]
        [SerializeField] private EquippedItemSlotUI equippedWeaponSlot;
        [SerializeField] private EquippedItemSlotUI equippedArmorSlot;
        [SerializeField] private EquippedItemSlotUI equippedRecipeSlot0;
        [SerializeField] private EquippedItemSlotUI equippedRecipeSlot1;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        // ── 只读属性 ──────────────────────────────────────────────────────

        public CanvasGroup CanvasGroup => canvasGroup;

        public Button[] CategoryTabButtons => categoryTabButtons;
        public GameObject[] CategoryTabHighlights => categoryTabHighlights;

        public Transform ItemGridRoot => itemGridRoot;
        public InventorySlotUI SlotPrefab => slotPrefab;
        public ScrollRect GridScrollRect => gridScrollRect;

        public Image DetailIcon => detailIcon;
        public TMP_Text DetailNameText => detailNameText;
        public TMP_Text DetailTypeText => detailTypeText;
        public TMP_Text DetailDescText => detailDescText;
        public Image DetailRarityBadgeBackground => detailRarityBadgeBackground;
        public TMP_Text DetailRarityBadgeText => detailRarityBadgeText;
        public Transform DetailModifiersRoot => detailModifiersRoot;
        public TMP_Text ModifierRowPrefab => modifierRowPrefab;
        public Button EquipButton => equipButton;
        public TMP_Text EquipButtonLabel => equipButtonLabel;

        public EquippedItemSlotUI EquippedWeaponSlot => equippedWeaponSlot;
        public EquippedItemSlotUI EquippedArmorSlot => equippedArmorSlot;
        public EquippedItemSlotUI EquippedRecipeSlot0 => equippedRecipeSlot0;
        public EquippedItemSlotUI EquippedRecipeSlot1 => equippedRecipeSlot1;

        public Button CloseButton => closeButton;
    }
}
