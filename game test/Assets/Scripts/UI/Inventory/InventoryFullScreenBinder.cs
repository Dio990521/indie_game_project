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

        [Header("分类 Tab（顺序必须与 InventoryTab 枚举一致：All / Equipment / Consumable / Material / Quest）")]
        [SerializeField] private Button[] categoryTabButtons;
        // 每个 Tab 对应的高亮 GameObject（选中时激活）
        [SerializeField] private GameObject[] categoryTabHighlights;

        [Header("物品格")]
        [SerializeField] private Transform itemGridRoot;
        [SerializeField] private InventorySlotUI slotPrefab;

        [Header("详情面板")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailTypeText;
        [SerializeField] private TMP_Text detailDescText;
        // 格式："25/100"（当前数量 / 最大堆叠）
        [SerializeField] private TMP_Text detailCountText;
        [SerializeField] private Button useButton;
        [SerializeField] private Button discardButton;

        [Header("底栏")]
        // 格式："25/48"（已用槽位 / 最大容量）
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private TMP_Text goldText;

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

        public Image DetailIcon => detailIcon;
        public TMP_Text DetailNameText => detailNameText;
        public TMP_Text DetailTypeText => detailTypeText;
        public TMP_Text DetailDescText => detailDescText;
        public TMP_Text DetailCountText => detailCountText;
        public Button UseButton => useButton;
        public Button DiscardButton => discardButton;

        public TMP_Text CapacityText => capacityText;
        public TMP_Text GoldText => goldText;

        public EquippedWeaponSlotUI EquippedWeaponSlot => equippedWeaponSlot;

        public Button CloseButton => closeButton;
    }
}
