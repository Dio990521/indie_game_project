using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 全屏背包界面的 UI 引用容器。
    /// 严格遵守 Binder 规范：仅暴露引用，不写任何逻辑。
    /// </summary>
    public class InventoryFullScreenBinder : MonoBehaviour
    {
        [Header("根节点")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("分类 Tab（顺序必须与 InventoryTab 枚举一致：Consumable(道具) / Material(材料) / Blueprint(图纸) / Quest(任务)）")]
        [SerializeField] private Button[] categoryTabButtons;
        // 每个 Tab 对应的高亮 GameObject（选中时激活）
        [SerializeField] private GameObject[] categoryTabHighlights;

        [Header("物品格")]
        [SerializeField] private Transform itemGridRoot;
        [SerializeField] private InventorySlotUI slotPrefab;
        // 物品网格所在的 ScrollRect（可选）：切换 Tab 时用于把滚动位置重置回顶部
        [SerializeField] private ScrollRect gridScrollRect;

        [Header("详情面板")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailTypeText;
        [SerializeField] private TMP_Text detailDescText;
        // 格式："25/100"（当前数量 / 最大堆叠）
        [SerializeField] private TMP_Text detailCountText;
        // 稀有度色块：背景色 + 文字（普通/优良/稀有/史诗/传说）
        [SerializeField] private Image detailRarityBadgeBackground;
        [SerializeField] private TMP_Text detailRarityBadgeText;
        [SerializeField] private Button useButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Button renameButton;

        [Header("底栏")]
        // 格式："25/48"（已用槽位 / 最大容量）
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private TMP_Text goldText;
        // 整理按钮：当前仅打印 log，不接排序逻辑
        [SerializeField] private Button sortButton;

        [Header("当前装备武器槽")]
        [SerializeField] private EquippedWeaponSlotUI equippedWeaponSlot;

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
        public TMP_Text DetailCountText => detailCountText;
        public Image DetailRarityBadgeBackground => detailRarityBadgeBackground;
        public TMP_Text DetailRarityBadgeText => detailRarityBadgeText;
        public Button UseButton => useButton;
        public Button DiscardButton => discardButton;
        public Button RenameButton => renameButton;

        public TMP_Text CapacityText => capacityText;
        public TMP_Text GoldText => goldText;
        public Button SortButton => sortButton;

        public EquippedWeaponSlotUI EquippedWeaponSlot => equippedWeaponSlot;

        public Button CloseButton => closeButton;
    }
}
