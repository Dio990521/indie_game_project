using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Economy;
using IndieGame.UI.Confirmation;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 全屏背包界面控制器。
    /// 负责分类过滤、槽位对象池、物品详情展示、使用/丢弃操作。
    /// 通过 InventoryManager 的静态事件与游戏状态机（PlayerTurnState）保持兼容。
    /// </summary>
    public class InventoryFullScreenController : EventBusMonoBehaviour
    {
        // 分类 Tab 枚举，顺序必须与 Binder 中 categoryTabButtons 数组对应
        private enum InventoryTab { All, Equipment, Consumable, Material, Quest }

        [SerializeField] private InventoryFullScreenBinder binder;
        // 对象池预热数量（避免首次打开时频繁 Instantiate）
        [SerializeField] private int slotPoolWarmup = 16;

        // ── 对象池 ───────────────────────────────────────────────────────
        private readonly List<InventorySlotUI> _slotPool = new List<InventorySlotUI>();
        // 当前激活的槽位数量（池中 [0, _activeSlotCount) 为活跃槽位）
        private int _activeSlotCount;

        // ── 状态 ────────────────────────────────────────────────────────
        private InventoryTab _currentTab = InventoryTab.All;
        private InventorySlot _selectedSlot;
        private CanvasGroup _canvasGroup;
        // 缓存最近一次 OnInventoryChanged 传入的槽位列表
        private IReadOnlyList<InventorySlot> _cachedSlots;
        // 缓存当前金币（由 GoldChangedEvent 持续更新）
        private int _currentGold;

        // ── 生命周期 ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[InventoryFullScreenController] Missing binder reference.");
                return;
            }

            _canvasGroup = binder.CanvasGroup;
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 初始隐藏
            SetVisible(false);

            // 预热对象池
            WarmupSlotPool();

            // 绑定按钮事件
            BindButtons();
        }

        protected override void OnEnable()
        {
            base.OnEnable(); // 触发 Bind()
            InventoryManager.OnInventoryOpened += HandleInventoryOpened;
            InventoryManager.OnInventoryClosed += HandleInventoryClosed;
        }

        protected override void OnDisable()
        {
            base.OnDisable(); // 触发 Unbind()
            InventoryManager.OnInventoryOpened -= HandleInventoryOpened;
            InventoryManager.OnInventoryClosed -= HandleInventoryClosed;
        }

        /// <summary>
        /// 通过 EventBusMonoBehaviour 的 Bind() 注册 EventBus 事件。
        /// </summary>
        protected override void Bind()
        {
            Subscribe<OnInventoryChanged>(HandleInventoryChanged);
            Subscribe<GoldChangedEvent>(HandleGoldChanged);
            Subscribe<CloseInventoryEvent>(HandleCloseInventoryEvent);
        }

        // ── EventBus 处理器 ──────────────────────────────────────────────

        private void HandleInventoryChanged(OnInventoryChanged evt)
        {
            _cachedSlots = evt.Slots;
            // 仅在背包可见时才重建（不可见时等打开时再刷新）
            if (_canvasGroup != null && _canvasGroup.alpha > 0f)
            {
                RebuildSlotList();
                RefreshCapacity();
            }
        }

        private void HandleGoldChanged(GoldChangedEvent evt)
        {
            _currentGold = evt.CurrentGold;
            RefreshGold();
        }

        private void HandleCloseInventoryEvent(CloseInventoryEvent evt)
        {
            CloseAndNotify();
        }

        // ── InventoryManager 静态事件处理器 ──────────────────────────────

        private void HandleInventoryOpened()
        {
            // 同步初始金币（避免 GoldChangedEvent 尚未触发时显示 0）
            _currentGold = GoldSystem.Instance != null ? GoldSystem.Instance.CurrentGold : _currentGold;

            SetVisible(true);
            RebuildSlotList();
            RefreshCapacity();
            RefreshGold();
        }

        private void HandleInventoryClosed()
        {
            SetVisible(false);
        }

        // ── 按钮绑定 ─────────────────────────────────────────────────────

        private void BindButtons()
        {
            if (binder == null) return;

            // Tab 按钮
            Button[] tabs = binder.CategoryTabButtons;
            if (tabs != null)
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    int tabIndex = i; // 闭包捕获
                    tabs[i]?.onClick.AddListener(() => SwitchTab((InventoryTab)tabIndex));
                }
            }

            binder.UseButton?.onClick.AddListener(HandleUseClicked);
            binder.DiscardButton?.onClick.AddListener(HandleDiscardClicked);
            binder.CloseButton?.onClick.AddListener(CloseAndNotify);
        }

        // ── 显示 / 隐藏 ──────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        /// <summary>
        /// 关闭背包并通知 InventoryManager（维持 PlayerTurnState 兼容性）。
        /// </summary>
        private void CloseAndNotify()
        {
            InventoryManager.Instance?.CloseInventory();
        }

        // ── 槽位对象池 ───────────────────────────────────────────────────

        private void WarmupSlotPool()
        {
            if (binder?.SlotPrefab == null || binder?.ItemGridRoot == null) return;

            for (int i = 0; i < slotPoolWarmup; i++)
            {
                InventorySlotUI slot = Instantiate(binder.SlotPrefab, binder.ItemGridRoot);
                slot.gameObject.SetActive(false);
                _slotPool.Add(slot);
            }
        }

        /// <summary>
        /// 从对象池取一个槽位 UI（不足时扩容）。
        /// </summary>
        private InventorySlotUI GetPooledSlot()
        {
            if (_activeSlotCount < _slotPool.Count)
            {
                InventorySlotUI existing = _slotPool[_activeSlotCount];
                existing.gameObject.SetActive(true);
                _activeSlotCount++;
                return existing;
            }

            // 池不足，扩容
            InventorySlotUI newSlot = Instantiate(binder.SlotPrefab, binder.ItemGridRoot);
            newSlot.gameObject.SetActive(true);
            _slotPool.Add(newSlot);
            _activeSlotCount++;
            return newSlot;
        }

        /// <summary>
        /// 将超出活跃数量的槽位归还对象池（SetActive(false)）。
        /// </summary>
        private void ReturnExcessToPool()
        {
            for (int i = _activeSlotCount; i < _slotPool.Count; i++)
            {
                _slotPool[i].gameObject.SetActive(false);
            }
        }

        // ── 槽位列表重建 ──────────────────────────────────────────────────

        /// <summary>
        /// 按当前 Tab 过滤槽位后重建 UI 列表。
        /// </summary>
        private void RebuildSlotList()
        {
            _activeSlotCount = 0;

            if (_cachedSlots != null)
            {
                for (int i = 0; i < _cachedSlots.Count; i++)
                {
                    InventorySlot slot = _cachedSlots[i];
                    if (slot == null || slot.Item == null) continue;
                    if (!SlotMatchesTab(slot)) continue;

                    InventorySlotUI slotUI = GetPooledSlot();
                    slotUI.Setup(slot, OnSlotClicked);
                }
            }

            ReturnExcessToPool();

            // 若当前选中项在新列表中已不存在，清空详情；否则刷新（如数量变化）
            if (_selectedSlot != null && !IsSelectedSlotStillValid())
            {
                _selectedSlot = null;
                ClearDetailPanel();
            }
            else if (_selectedSlot != null)
            {
                RefreshDetailPanel();
                RefreshActionButtons();
            }
        }

        /// <summary>
        /// 检查 _selectedSlot 是否仍在 _cachedSlots 中。
        /// </summary>
        private bool IsSelectedSlotStillValid()
        {
            if (_cachedSlots == null || _selectedSlot == null) return false;
            for (int i = 0; i < _cachedSlots.Count; i++)
            {
                if (_cachedSlots[i] == _selectedSlot) return true;
            }
            return false;
        }

        /// <summary>
        /// 判断槽位是否符合当前 Tab 过滤条件。
        /// </summary>
        private bool SlotMatchesTab(InventorySlot slot)
        {
            return _currentTab switch
            {
                InventoryTab.Equipment  => slot.Item.Category == ItemCategory.Equipment,
                InventoryTab.Consumable => slot.Item.Category == ItemCategory.Consumable,
                InventoryTab.Material   => slot.Item.Category == ItemCategory.Material,
                InventoryTab.Quest      => slot.Item.Category == ItemCategory.Quest,
                _                       => true // All
            };
        }

        // ── Tab 切换 ─────────────────────────────────────────────────────

        private void SwitchTab(InventoryTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            RefreshTabHighlights();
            RebuildSlotList();
            // 切换 Tab 时清空详情（避免展示与当前 Tab 无关的物品）
            _selectedSlot = null;
            ClearDetailPanel();
        }

        private void RefreshTabHighlights()
        {
            GameObject[] highlights = binder?.CategoryTabHighlights;
            if (highlights == null) return;
            int activeIndex = (int)_currentTab;
            for (int i = 0; i < highlights.Length; i++)
            {
                if (highlights[i] != null)
                    highlights[i].SetActive(i == activeIndex);
            }
        }

        // ── 槽位选中 & 详情面板 ──────────────────────────────────────────

        private void OnSlotClicked(InventorySlot slot)
        {
            _selectedSlot = slot;
            RefreshDetailPanel();
            RefreshActionButtons();
        }

        /// <summary>
        /// 更新右侧详情面板的所有文本和图标。
        /// </summary>
        private void RefreshDetailPanel()
        {
            if (binder == null || _selectedSlot == null || _selectedSlot.Item == null)
            {
                ClearDetailPanel();
                return;
            }

            ItemSO item = _selectedSlot.Item;

            // 图标
            if (binder.DetailIcon != null)
                binder.DetailIcon.sprite = item.Icon;

            // 名称：优先显示实例自定义名，否则异步读取本地化名
            if (binder.DetailNameText != null)
            {
                if (!string.IsNullOrWhiteSpace(_selectedSlot.CustomName))
                {
                    binder.DetailNameText.text = _selectedSlot.CustomName;
                }
                else if (item.ItemName != null)
                {
                    var handle = item.ItemName.GetLocalizedStringAsync();
                    handle.Completed += op =>
                    {
                        // 防止异步回调时选中项已变更
                        if (_selectedSlot?.Item != item) return;
                        if (binder.DetailNameText != null)
                            binder.DetailNameText.text = op.Result;
                    };
                }
                else
                {
                    binder.DetailNameText.text = string.IsNullOrWhiteSpace(item.ID) ? "Unknown" : item.ID;
                }
            }

            // 分类
            if (binder.DetailTypeText != null)
                binder.DetailTypeText.text = CategoryToDisplayName(item.Category);

            // 描述
            if (binder.DetailDescText != null)
                binder.DetailDescText.text = item.Description ?? string.Empty;

            // 数量（当前数 / 最大堆叠）
            if (binder.DetailCountText != null)
            {
                int maxStack = item.isStackable ? item.maxStack : 1;
                binder.DetailCountText.text = $"{_selectedSlot.Count}/{maxStack}";
            }
        }

        private void ClearDetailPanel()
        {
            if (binder == null) return;
            if (binder.DetailIcon != null) binder.DetailIcon.sprite = null;
            if (binder.DetailNameText != null) binder.DetailNameText.text = string.Empty;
            if (binder.DetailTypeText != null) binder.DetailTypeText.text = string.Empty;
            if (binder.DetailDescText != null) binder.DetailDescText.text = string.Empty;
            if (binder.DetailCountText != null) binder.DetailCountText.text = string.Empty;
            RefreshActionButtons();
        }

        /// <summary>
        /// 根据选中项的分类决定按钮的可用状态。
        /// Use 按钮：仅消耗品可用；Discard 按钮：选中时始终可用。
        /// </summary>
        private void RefreshActionButtons()
        {
            bool hasSelection = _selectedSlot != null && _selectedSlot.Item != null;
            bool isConsumable = hasSelection && _selectedSlot.Item.Category == ItemCategory.Consumable;

            if (binder.UseButton != null)
                binder.UseButton.interactable = isConsumable;

            if (binder.DiscardButton != null)
                binder.DiscardButton.interactable = hasSelection;
        }

        // ── 操作按钮 ─────────────────────────────────────────────────────

        private void HandleUseClicked()
        {
            if (_selectedSlot == null || _selectedSlot.Item == null) return;
            // UseItem 内部：调用 item.Use()，消耗品自动 RemoveItem(1)，最终触发 OnInventoryChanged
            InventoryManager.Instance?.UseItem(_selectedSlot.Item);
        }

        private void HandleDiscardClicked()
        {
            if (_selectedSlot == null || _selectedSlot.Item == null) return;

            // 获取物品显示名（同步读取 ID 作为兜底，异步本地化名在弹窗中不需要等待）
            string itemName = string.IsNullOrWhiteSpace(_selectedSlot.CustomName)
                ? (_selectedSlot.Item.ID ?? "该物品")
                : _selectedSlot.CustomName;

            // 捕获当前选中项引用，防止弹窗等待期间选中项切换
            InventorySlot slotToDiscard = _selectedSlot;

            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = $"确认丢弃 {itemName} x1？",
                OnConfirm = () =>
                {
                    if (slotToDiscard?.Item == null) return;
                    InventoryManager.Instance?.RemoveItem(slotToDiscard.Item, 1);
                },
                OnCancel = null
            });
        }

        // ── 底栏刷新 ─────────────────────────────────────────────────────

        private void RefreshCapacity()
        {
            if (binder?.CapacityText == null) return;
            int used = _cachedSlots?.Count ?? 0;
            int max = InventoryManager.Instance != null ? InventoryManager.Instance.maxCapacity : 0;
            binder.CapacityText.text = $"Bag: {used}/{max}";
        }

        private void RefreshGold()
        {
            if (binder?.GoldText == null) return;
            binder.GoldText.text = _currentGold.ToString("N0");
        }

        // ── 辅助 ─────────────────────────────────────────────────────────

        private static string CategoryToDisplayName(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Equipment  => "Equipment",
                ItemCategory.Consumable => "Consumable",
                ItemCategory.Material   => "Material",
                ItemCategory.Quest      => "Quest Item",
                _                       => "Unknown"
            };
        }
    }
}
