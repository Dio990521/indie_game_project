using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Equipment;
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
        // 分类 Tab 枚举，顺序必须与 Binder 中 categoryTabButtons 数组对应：道具/材料/图纸/任务
        private enum InventoryTab { Consumable, Material, Blueprint, Quest }

        [SerializeField] private InventoryFullScreenBinder binder;
        [SerializeField] private GameInputReader inputReader;
        // 对象池预热数量；同时也是网格的最小展示槛位数（物品不足时用空槛位补齐占位，超出时靠 ScrollRect 滚动查看）
        [SerializeField] private int slotPoolWarmup = 28;

        // ── 对象池 ───────────────────────────────────────────────────────
        private readonly List<InventorySlotUI> _slotPool = new List<InventorySlotUI>();
        // 当前激活的槽位数量（池中 [0, _activeSlotCount) 为活跃槽位）
        private int _activeSlotCount;

        // ── 状态 ────────────────────────────────────────────────────────
        private InventoryTab _currentTab = InventoryTab.Consumable;
        private InventorySlot _selectedSlot;
        private CanvasGroup _canvasGroup;
        // 缓存最近一次 OnInventoryChanged 传入的槽位列表
        private IReadOnlyList<InventorySlot> _cachedSlots;
        // 缓存当前金币（由 GoldChangedEvent 持续更新）
        private int _currentGold;
        // 玩家身上的武器装备控制器（懒解析，随 CurrentPlayer 变化重新解析）
        private WeaponEquipController _playerWeaponEquip;
        private GameObject _playerWeaponEquipOwner;
        // 改名弹窗请求上下文
        private int _popupRequestSeed;
        private int _pendingRenameRequestId = -1;
        private InventorySlot _renameTargetSlot;
        // ESC/手柄 Cancel 关闭绑定（与关闭按钮共用 CloseAndNotify）
        private EscCloseBinding _escBinding;

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

            _escBinding = new EscCloseBinding(inputReader, () => _canvasGroup != null && _canvasGroup.alpha > 0f, CloseAndNotify);
        }

        protected override void OnEnable()
        {
            // 父类调用 Bind() 自动订阅所有 EventBus 事件
            base.OnEnable();
            _escBinding?.Subscribe();
        }

        protected override void OnDisable()
        {
            // 父类自动取消订阅所有 EventBus 事件
            base.OnDisable();
            _escBinding?.Unsubscribe();
        }

        /// <summary>
        /// 通过 EventBusMonoBehaviour 的 Bind() 注册 EventBus 事件。
        /// 旧的 InventoryManager.OnInventoryOpened/Closed 静态委托已废弃，
        /// 改为统一 Subscribe 到 EventBus 的 InventoryOpenedEvent/InventoryClosedEvent，
        /// 反订阅由 EventBusMonoBehaviour 在 OnDisable 中自动完成。
        /// </summary>
        protected override void Bind()
        {
            Subscribe<OnInventoryChanged>(HandleInventoryChanged);
            Subscribe<GoldChangedEvent>(HandleGoldChanged);
            Subscribe<CloseInventoryEvent>(HandleCloseInventoryEvent);
            Subscribe<InventoryOpenedEvent>(HandleInventoryOpened);
            Subscribe<InventoryClosedEvent>(HandleInventoryClosed);
            Subscribe<WeaponEquippedEvent>(HandleWeaponEquipChanged);
            Subscribe<WeaponUnequippedEvent>(HandleWeaponUnequipChanged);
            Subscribe<RenameSlotPopupResultEvent>(HandleRenamePopupResult);
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

        private void HandleWeaponEquipChanged(WeaponEquippedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            RefreshEquippedWeaponSlot();
            RefreshActionButtons();
        }

        private void HandleWeaponUnequipChanged(WeaponUnequippedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            RefreshEquippedWeaponSlot();
            RefreshActionButtons();
        }

        /// <summary>
        /// 若目标槽位正是当前选中/已装备的武器，刷新对应展示（改名等操作后调用）。
        /// </summary>
        private void RefreshIfAffected(InventorySlot slot)
        {
            if (slot == null) return;
            if (_selectedSlot == slot) RefreshDetailPanel();
            if (TryBindPlayerWeaponEquip() && _playerWeaponEquip.CurrentWeaponSlot == slot) RefreshEquippedWeaponSlot();
        }

        // ── InventoryManager 静态事件处理器 ──────────────────────────────

        private void HandleInventoryOpened(InventoryOpenedEvent evt)
        {
            // 同步初始金币（避免 GoldChangedEvent 尚未触发时显示 0）
            _currentGold = GoldSystem.Instance != null ? GoldSystem.Instance.CurrentGold : _currentGold;

            // 置顶到同层最后，确保覆盖 CampUI、PlayerHUD 等同层节点
            transform.SetAsLastSibling();
            SetVisible(true);
            RebuildSlotList();
            RefreshCapacity();
            RefreshGold();
            RefreshEquippedWeaponSlot();
        }

        private void HandleInventoryClosed(InventoryClosedEvent evt)
        {
            SetVisible(false);
            ClearPendingRenameRequest();
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
            binder.RenameButton?.onClick.AddListener(HandleRenameButtonClicked);
            binder.SortButton?.onClick.AddListener(HandleSortClicked);
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
        /// 网格按固定最小数量（slotPoolWarmup）展示：物品数不足时用空槛位（Setup(null,...)）补齐占位，
        /// 超出时不截断，靠 ScrollRect 滚动查看（不在此处处理滚动裁剪）。
        /// </summary>
        private void RebuildSlotList()
        {
            _activeSlotCount = 0;

            int filledCount = 0;
            if (_cachedSlots != null)
            {
                for (int i = 0; i < _cachedSlots.Count; i++)
                {
                    InventorySlot slot = _cachedSlots[i];
                    if (slot == null || slot.Item == null) continue;
                    if (!SlotMatchesTab(slot)) continue;

                    InventorySlotUI slotUI = GetPooledSlot();
                    slotUI.Setup(slot, OnSlotClicked);
                    filledCount++;
                }
            }

            // 补齐空槛位占位，网格至少显示 slotPoolWarmup 个格子
            for (int i = filledCount; i < slotPoolWarmup; i++)
            {
                InventorySlotUI emptySlotUI = GetPooledSlot();
                emptySlotUI.Setup(null, null);
            }

            ReturnExcessToPool();
            RefreshSelectionHighlight();

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
        /// 刷新槛位选中高亮：遍历当前活跃槛位，仅让绑定了 _selectedSlot 的那一个显示高亮。
        /// </summary>
        private void RefreshSelectionHighlight()
        {
            for (int i = 0; i < _activeSlotCount; i++)
            {
                InventorySlotUI slotUI = _slotPool[i];
                slotUI.SetSelected(_selectedSlot != null && slotUI.BoundSlot == _selectedSlot);
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
                InventoryTab.Consumable => slot.Item.Category == ItemCategory.Consumable,
                InventoryTab.Material   => slot.Item.Category == ItemCategory.Material,
                InventoryTab.Blueprint  => slot.Item.Category == ItemCategory.Blueprint,
                InventoryTab.Quest      => slot.Item.Category == ItemCategory.Quest,
                _                       => false
            };
        }

        // ── Tab 切换 ─────────────────────────────────────────────────────

        private void SwitchTab(InventoryTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            RefreshTabHighlights();
            // 切换 Tab 时清空详情（避免展示与当前 Tab 无关的物品）
            _selectedSlot = null;
            ClearDetailPanel();
            RebuildSlotList();

            // 切回顶部，避免停留在上一个 Tab 的滚动位置
            if (binder?.GridScrollRect != null)
                binder.GridScrollRect.verticalNormalizedPosition = 1f;
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
            RefreshSelectionHighlight();
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

            // 名称：优先使用槽位自定义名，否则回退本地化名称
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
                        // 防御性校验顺序：
                        // 1) Controller 自身或 binder 已被销毁 → 直接放弃；
                        // 2) 选中项已变更 → 旧异步结果丢弃，避免回写错位的名称。
                        if (this == null || binder == null) return;
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

            // 稀有度色块：背景色 + 中文短标签（普通/优良/稀有/史诗/传说）
            if (binder.DetailRarityBadgeBackground != null)
                binder.DetailRarityBadgeBackground.color = ItemRarityUtility.GetColor(item.Rarity);
            if (binder.DetailRarityBadgeText != null)
                binder.DetailRarityBadgeText.text = ItemRarityUtility.GetDisplayName(item.Rarity);

            // 描述
            if (binder.DetailDescText != null)
                binder.DetailDescText.text = item.Description ?? string.Empty;

            // 持有数量
            if (binder.DetailCountText != null)
                binder.DetailCountText.text = _selectedSlot.Count.ToString();
        }

        private void ClearDetailPanel()
        {
            if (binder == null) return;
            if (binder.DetailIcon != null) binder.DetailIcon.sprite = null;
            if (binder.DetailNameText != null) binder.DetailNameText.text = string.Empty;
            if (binder.DetailTypeText != null) binder.DetailTypeText.text = string.Empty;
            if (binder.DetailDescText != null) binder.DetailDescText.text = string.Empty;
            if (binder.DetailCountText != null) binder.DetailCountText.text = string.Empty;
            if (binder.DetailRarityBadgeText != null) binder.DetailRarityBadgeText.text = string.Empty;
            RefreshActionButtons();
        }

        /// <summary>
        /// 根据选中项的分类决定按钮的可用状态。
        /// Use 按钮：消耗品或武器可用（武器走装备/卸下逻辑）；Discard 按钮：选中时始终可用。
        /// </summary>
        private void RefreshActionButtons()
        {
            bool hasSelection = _selectedSlot != null && _selectedSlot.Item != null;
            bool isConsumable = hasSelection && _selectedSlot.Item.Category == ItemCategory.Consumable;
            bool isWeapon = hasSelection && _selectedSlot.Item is WeaponSO;

            if (binder.UseButton != null)
                binder.UseButton.interactable = isConsumable || isWeapon;

            if (binder.DiscardButton != null)
                binder.DiscardButton.interactable = hasSelection;

            if (binder.RenameButton != null)
                binder.RenameButton.interactable = hasSelection;
        }

        // ── 操作按钮 ─────────────────────────────────────────────────────

        private void HandleUseClicked()
        {
            if (_selectedSlot == null || _selectedSlot.Item == null) return;

            // 武器走装备/卸下切换，不进入消耗品的 UseItem 流程（武器不应被消耗）
            if (_selectedSlot.Item is WeaponSO)
            {
                ToggleEquipWeapon(_selectedSlot);
                return;
            }

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

        /// <summary>
        /// 整理按钮：当前仅打印 log，排序逻辑暂不接入（InventoryManager 已有 SortByCategory/SortByID 可用，待后续设计确认后再接）。
        /// </summary>
        private void HandleSortClicked()
        {
            DebugTools.Log("[InventoryFullScreenController] 整理按钮点击（排序逻辑未接入）");
        }

        /// <summary>
        /// 武器装备/卸下切换：未装备时装备它；已装备时卸下。由"使用"按钮触发。
        /// </summary>
        private void ToggleEquipWeapon(InventorySlot weaponSlot)
        {
            if (!TryBindPlayerWeaponEquip()) return;

            if (_playerWeaponEquip.CurrentWeaponSlot == weaponSlot)
            {
                _playerWeaponEquip.Unequip();
            }
            else
            {
                _playerWeaponEquip.Equip(weaponSlot);
            }
        }

        // ── 改名（背包详情面板，与 Craft 强化面板共用同一套事件） ──────────

        private void HandleRenameButtonClicked()
        {
            if (_selectedSlot == null || _selectedSlot.Item == null) return;

            string defaultName = !string.IsNullOrWhiteSpace(_selectedSlot.CustomName)
                ? _selectedSlot.CustomName
                : _selectedSlot.Item.GetLocalizedName();

            // 捕获改名目标槽位，弹窗结果回传时按 RequestId 匹配
            _renameTargetSlot = _selectedSlot;
            _pendingRenameRequestId = ++_popupRequestSeed;

            EventBus.Raise(new RenameSlotPopupRequestEvent
            {
                RequestId = _pendingRenameRequestId,
                DefaultName = defaultName
            });
        }

        private void HandleRenamePopupResult(RenameSlotPopupResultEvent evt)
        {
            if (evt.RequestId != _pendingRenameRequestId) return;

            InventorySlot slot = _renameTargetSlot;
            ClearPendingRenameRequest();

            if (!evt.Confirmed || slot == null) return;

            InventoryManager.Instance?.RenameSlot(slot, evt.CustomName);
            RefreshIfAffected(slot);
        }

        private void ClearPendingRenameRequest()
        {
            _pendingRenameRequestId = -1;
            _renameTargetSlot = null;
        }

        // ── 当前装备武器槽 ───────────────────────────────────────────────

        private void RefreshEquippedWeaponSlot()
        {
            if (binder?.EquippedWeaponSlot == null) return;

            WeaponSO weapon = TryBindPlayerWeaponEquip() ? _playerWeaponEquip.CurrentWeapon : null;
            binder.EquippedWeaponSlot.Refresh(weapon, weapon != null ? HandleUnequipSlotClicked : (System.Action)null);
            if (weapon == null) return;

            // 优先展示玩家自定义名，覆盖 Refresh 里设置的默认本地化名称
            InventorySlot equippedSlot = _playerWeaponEquip.CurrentWeaponSlot;
            if (!string.IsNullOrWhiteSpace(equippedSlot?.CustomName))
                binder.EquippedWeaponSlot.ApplyDisplayNameOverride(equippedSlot.CustomName);
        }

        private void HandleUnequipSlotClicked()
        {
            _playerWeaponEquip?.Unequip();
        }

        // ── 玩家引用解析 ─────────────────────────────────────────────────

        private bool TryBindPlayerWeaponEquip()
        {
            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            if (player == null) return false;
            if (_playerWeaponEquipOwner == player && _playerWeaponEquip != null) return true;

            _playerWeaponEquipOwner = player;
            _playerWeaponEquip = player.GetComponent<WeaponEquipController>();
            return _playerWeaponEquip != null;
        }

        private bool IsCurrentPlayer(GameObject owner)
        {
            if (owner == null) return false;
            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            return owner == player;
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
                ItemCategory.Blueprint  => "图纸",
                ItemCategory.Quest      => "Quest Item",
                _                       => "Unknown"
            };
        }
    }
}
