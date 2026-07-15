using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包详情面板（普通类，非 MonoBehaviour）：
    /// 负责右侧详情区的全部渲染——图标/名称（含异步本地化回填）/分类/稀有度/描述/数量，
    /// 以及 使用/丢弃/改名 三个操作按钮的可用状态。
    ///
    /// 拆分动机：与打造界面的 CraftingDetailPanel 对齐"Controller + ListManager/Pool + DetailPanel"
    /// 三件套模式——InventoryFullScreenController 原先一个类同时管理槽位池、Tab 过滤、
    /// 详情渲染、按钮流程，详情渲染（约 120 行）是其中独立性最强的一块。
    ///
    /// 边界约定：
    /// - 本类只"渲染"，不发起任何业务（按钮点击回调仍由 Controller 绑定与处理）；
    /// - 通过 Init 注入 Binder，Binder 为 MonoBehaviour，销毁后 == null 判断依然有效，
    ///   异步本地化回填以此做失效保护。
    /// </summary>
    public class InventoryDetailPanel
    {
        private InventoryFullScreenBinder _binder;
        // 当前展示的槽位：异步本地化回填时用于丢弃"选中项已切换"的过期结果
        private InventorySlot _currentSlot;

        /// <summary>
        /// 注入 UI 引用（Controller 的 Awake 中调用一次）。
        /// </summary>
        public void Init(InventoryFullScreenBinder binder)
        {
            _binder = binder;
        }

        /// <summary>
        /// 展示指定槽位的详情（null 或空槽位等价于 Clear）。
        /// </summary>
        public void Show(InventorySlot slot)
        {
            if (_binder == null) return;
            if (slot == null || slot.Item == null)
            {
                Clear();
                return;
            }

            _currentSlot = slot;
            ItemSO item = slot.Item;

            // 图标
            if (_binder.DetailIcon != null)
                _binder.DetailIcon.sprite = item.Icon;

            // 名称：优先使用槽位自定义名，否则回退本地化名称
            if (_binder.DetailNameText != null)
            {
                if (!string.IsNullOrWhiteSpace(slot.CustomName))
                {
                    _binder.DetailNameText.text = slot.CustomName;
                }
                else if (item.ItemName != null)
                {
                    var handle = item.ItemName.GetLocalizedStringAsync();
                    handle.Completed += op =>
                    {
                        // 防御性校验顺序：
                        // 1) Binder（Unity 对象）已被销毁 → 直接放弃；
                        // 2) 展示项已变更 → 旧异步结果丢弃，避免回写错位的名称。
                        if (_binder == null || _binder.DetailNameText == null) return;
                        if (_currentSlot?.Item != item) return;
                        _binder.DetailNameText.text = op.Result;
                    };
                }
                else
                {
                    _binder.DetailNameText.text = string.IsNullOrWhiteSpace(item.ID) ? "Unknown" : item.ID;
                }
            }

            // 分类
            if (_binder.DetailTypeText != null)
                _binder.DetailTypeText.text = CategoryToDisplayName(item.Category);

            // 稀有度色块：背景色 + 短标签
            if (_binder.DetailRarityBadgeBackground != null)
                _binder.DetailRarityBadgeBackground.color = ItemRarityUtility.GetColor(item.Rarity);
            if (_binder.DetailRarityBadgeText != null)
                _binder.DetailRarityBadgeText.text = ItemRarityUtility.GetDisplayName(item.Rarity);

            // 描述
            if (_binder.DetailDescText != null)
                _binder.DetailDescText.text = item.Description ?? string.Empty;

            // 持有数量
            if (_binder.DetailCountText != null)
                _binder.DetailCountText.text = slot.Count.ToString();

            RefreshActionButtons(slot);
        }

        /// <summary>
        /// 清空详情面板并禁用操作按钮。
        /// </summary>
        public void Clear()
        {
            _currentSlot = null;
            if (_binder == null) return;
            if (_binder.DetailIcon != null) _binder.DetailIcon.sprite = null;
            if (_binder.DetailNameText != null) _binder.DetailNameText.text = string.Empty;
            if (_binder.DetailTypeText != null) _binder.DetailTypeText.text = string.Empty;
            if (_binder.DetailDescText != null) _binder.DetailDescText.text = string.Empty;
            if (_binder.DetailCountText != null) _binder.DetailCountText.text = string.Empty;
            if (_binder.DetailRarityBadgeText != null) _binder.DetailRarityBadgeText.text = string.Empty;
            RefreshActionButtons(null);
        }

        /// <summary>
        /// 根据选中项的分类决定按钮的可用状态。
        /// Use 按钮：消耗品或武器可用（武器走装备/卸下逻辑）；Discard/Rename 按钮：选中时可用。
        /// </summary>
        public void RefreshActionButtons(InventorySlot selected)
        {
            if (_binder == null) return;

            bool hasSelection = selected != null && selected.Item != null;
            bool isConsumable = hasSelection && selected.Item.Category == ItemCategory.Consumable;
            bool isWeapon = hasSelection && selected.Item is WeaponSO;

            if (_binder.UseButton != null)
                _binder.UseButton.interactable = isConsumable || isWeapon;

            if (_binder.DiscardButton != null)
                _binder.DiscardButton.interactable = hasSelection;

            if (_binder.RenameButton != null)
                _binder.RenameButton.interactable = hasSelection;
        }

        /// <summary>
        /// 分类显示名（统一走 UIText 目录）。
        /// </summary>
        private static string CategoryToDisplayName(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Equipment  => UIText.CategoryEquipment,
                ItemCategory.Consumable => UIText.CategoryConsumable,
                ItemCategory.Material   => UIText.CategoryMaterial,
                ItemCategory.Blueprint  => UIText.CategoryBlueprint,
                ItemCategory.Quest      => UIText.CategoryQuest,
                _                       => UIText.CategoryUnknown
            };
        }
    }
}
