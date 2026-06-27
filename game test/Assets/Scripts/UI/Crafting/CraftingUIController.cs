using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Equipment;
using IndieGame.Gameplay.Inventory;
using IndieGame.UI.Confirmation;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 打造界面协调器（薄控制层）：
    /// 负责界面显隐、Tab切换、弹窗流程、EventBus订阅与输入处理。
    ///
    /// 架构边界：
    /// - CraftUIBinder：只保存引用，不写逻辑。
    /// - CraftingListManager：管理左侧列表对象池、条目索引、构建逻辑。
    /// - CraftingDetailPanel：管理右侧详情面板、需求槽位池、按钮状态。
    /// - WeaponEnhanceListManager/WeaponPrefixListManager/WeaponEnhanceDetailPanel：强化 Tab 的对应职责。
    /// - CraftingUIController（本类）：协调上述两者，处理生命周期与事件路由。
    /// - CraftingSystem/WeaponEnhanceSystem：只负责规则与数据（扣料、产出、历史、存档）。
    /// </summary>
    public class CraftingUIController : EventBusMonoBehaviour
    {
        /// <summary>
        /// Tab 类型：Prototype 显示未消耗蓝图，Replication 显示历史记录，Enhance 是武器强化。
        /// </summary>
        private enum CraftTab { Prototype, Replication, Enhance }

        [Header("References")]
        [SerializeField] private CraftUIBinder binder;
        [SerializeField] private GameInputReader inputReader;

        [Header("Pool Settings")]
        [Tooltip("左侧图纸列表对象池预热数量")]
        [SerializeField] private int slotPoolWarmup = 8;
        [Tooltip("右侧材料列表对象池预热数量")]
        [SerializeField] private int requirementPoolWarmup = 10;

        private CraftingListManager _listManager;
        private CraftingDetailPanel _detailPanel;

        private WeaponEnhanceListManager _weaponListManager;
        private WeaponPrefixListManager _prefixListManager;
        private WeaponEnhanceDetailPanel _weaponEnhanceDetailPanel;
        // 当前选中的待强化/重铸前缀词（语料库列表里点选的那一个）
        private string _selectedPrefixWordId;
        // 当前选中的"重铸目标"前缀位序号；-1 表示未进入重铸选择
        private int _rebindIndex = -1;
        // 改名弹窗的待处理槽位（区分背包/装备中的武器都可能触发改名）
        private InventorySlot _pendingRenameSlot;

        private CraftTab _currentTab = CraftTab.Prototype;
        private bool _isVisible;
        private CanvasGroup _canvasGroup;

        // 弹窗请求上下文（通过 RequestId 匹配响应，避免多次点击串线）
        private int _popupRequestSeed;
        private int _pendingPopupRequestId = -1;
        private string _pendingBlueprintId;
        private string _pendingDefaultName;
        private int _pendingRenameRequestId = -1;

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[CraftingUIController] Missing CraftUIBinder reference.");
                return;
            }

            _listManager = new CraftingListManager();
            _detailPanel = new CraftingDetailPanel();
            _listManager.Init(binder, slotPoolWarmup);
            _detailPanel.Init(binder, requirementPoolWarmup);

            _weaponListManager = new WeaponEnhanceListManager();
            _prefixListManager = new WeaponPrefixListManager();
            _weaponEnhanceDetailPanel = new WeaponEnhanceDetailPanel();
            _weaponListManager.Init(binder, slotPoolWarmup);
            _prefixListManager.Init(binder, requirementPoolWarmup);
            _weaponEnhanceDetailPanel.Init(binder, requirementPoolWarmup);

            EnsureCanvasGroup();
            ApplyTabContentVisibility();

            if (binder.CraftButton != null) binder.CraftButton.onClick.AddListener(HandleCraftButtonClicked);
            if (binder.PrototypeTabButton != null) binder.PrototypeTabButton.onClick.AddListener(HandlePrototypeTabClicked);
            if (binder.ReplicationTabButton != null) binder.ReplicationTabButton.onClick.AddListener(HandleReplicationTabClicked);
            if (binder.EnhanceTabButton != null) binder.EnhanceTabButton.onClick.AddListener(HandleEnhanceTabClicked);
            if (binder.EnhanceConfirmButton != null) binder.EnhanceConfirmButton.onClick.AddListener(HandleEnhanceConfirmClicked);
            if (binder.RebindConfirmButton != null) binder.RebindConfirmButton.onClick.AddListener(HandleRebindConfirmClicked);
            if (binder.RenameButton != null) binder.RenameButton.onClick.AddListener(HandleRenameButtonClicked);
        }

        private void OnDestroy()
        {
            if (binder == null) return;
            if (binder.CraftButton != null) binder.CraftButton.onClick.RemoveListener(HandleCraftButtonClicked);
            if (binder.PrototypeTabButton != null) binder.PrototypeTabButton.onClick.RemoveListener(HandlePrototypeTabClicked);
            if (binder.ReplicationTabButton != null) binder.ReplicationTabButton.onClick.RemoveListener(HandleReplicationTabClicked);
            if (binder.EnhanceTabButton != null) binder.EnhanceTabButton.onClick.RemoveListener(HandleEnhanceTabClicked);
            if (binder.EnhanceConfirmButton != null) binder.EnhanceConfirmButton.onClick.RemoveListener(HandleEnhanceConfirmClicked);
            if (binder.RebindConfirmButton != null) binder.RebindConfirmButton.onClick.RemoveListener(HandleRebindConfirmClicked);
            if (binder.RenameButton != null) binder.RenameButton.onClick.RemoveListener(HandleRenameButtonClicked);
        }

        protected override void OnEnable()
        {
            // 父类调用 Bind() 自动订阅所有 EventBus 事件
            base.OnEnable();
            SubscribeInput();
            // 重要：默认隐藏，等待 OpenCraftingUIEvent 再显示
            SetVisible(false);
        }

        protected override void OnDisable()
        {
            // 父类自动取消订阅所有 EventBus 事件
            base.OnDisable();
            UnsubscribeInput();
            _listManager.ReleaseAll();
            _detailPanel.ReleaseAllRequirementSlots();
            _weaponListManager.ReleaseAll();
            _prefixListManager.ReleaseAll();
            _weaponEnhanceDetailPanel.ReleaseAllAppliedPrefixSlots();
            ClearPendingPopupRequest();
            ClearPendingRenameRequest();
        }

        // --- Tab 切换 ---

        private void HandlePrototypeTabClicked() => SwitchTab(CraftTab.Prototype);
        private void HandleReplicationTabClicked() => SwitchTab(CraftTab.Replication);
        private void HandleEnhanceTabClicked() => SwitchTab(CraftTab.Enhance);

        private void SwitchTab(CraftTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            ApplyTabContentVisibility();
            if (_isVisible) RebuildCraftList();
        }

        /// <summary>
        /// Prototype/Replication 共用左侧图纸列表+右侧需求详情区域；Enhance 是完全不同的布局，
        /// 因此用一个独立根节点整体切换，而不是复用 ListRoot/RequirementsRoot。
        /// </summary>
        private void ApplyTabContentVisibility()
        {
            bool isEnhance = _currentTab == CraftTab.Enhance;
            if (binder.StandardTabContentRoot != null) binder.StandardTabContentRoot.SetActive(!isEnhance);
            if (binder.EnhanceRootNode != null) binder.EnhanceRootNode.SetActive(isEnhance);
        }

        // --- 列表重建 ---

        private void RebuildCraftList()
        {
            if (_currentTab == CraftTab.Enhance)
            {
                RebuildEnhanceTab();
                return;
            }

            CraftingSystem cs = CraftingSystem.Instance;
            bool hasEntries = _currentTab == CraftTab.Prototype
                ? _listManager.RebuildForPrototype(cs)
                : _listManager.RebuildForReplication(cs);

            if (hasEntries)
                OnEntrySelected(_listManager.EntryOrder[0]);
            else
                _detailPanel.EnterEmptyState();
        }

        // --- 条目选中（协调 ListManager 与 DetailPanel）---

        private void OnEntrySelected(string entryKey)
        {
            CraftingSystem cs = CraftingSystem.Instance;
            if (cs == null || !_listManager.TryGetEntry(entryKey, out CraftingListManager.CraftListEntry entry))
            {
                _detailPanel.EnterEmptyState();
                return;
            }

            BlueprintSO blueprint = cs.GetBlueprint(entry.BlueprintID);
            if (blueprint == null)
            {
                _detailPanel.EnterEmptyState();
                return;
            }

            _listManager.Select(entryKey);
            _detailPanel.ShowForEntry(blueprint);
            _detailPanel.RefreshRequirementList(entry.BlueprintID);
            _detailPanel.RefreshButtonState(entry.BlueprintID);
        }

        // --- 制造按钮 ---

        private void HandleCraftButtonClicked()
        {
            if (!_isVisible || _pendingPopupRequestId >= 0) return;

            string blueprintId = _listManager.GetSelectedBlueprintId();
            if (string.IsNullOrWhiteSpace(blueprintId)) return;

            CraftingSystem cs = CraftingSystem.Instance;
            if (cs == null) return;

            // 复现制造分支：直接使用历史名称，不弹输入框
            if (_currentTab == CraftTab.Replication)
            {
                string replicationName = _listManager.GetSelectedSuggestedName();
                if (string.IsNullOrWhiteSpace(replicationName))
                {
                    BlueprintSO fb = cs.GetBlueprint(blueprintId);
                    if (fb == null) return;
                    replicationName = cs.GetOriginalProductName(fb);
                }
                cs.ExecuteCraft(blueprintId, replicationName);
                return;
            }

            BlueprintSO blueprint = cs.GetBlueprint(blueprintId);
            if (blueprint == null) return;

            string defaultName = _listManager.GetSelectedSuggestedName();
            if (string.IsNullOrWhiteSpace(defaultName))
                defaultName = cs.GetOriginalProductName(blueprint);

            _pendingPopupRequestId = ++_popupRequestSeed;
            _pendingBlueprintId    = blueprintId;
            _pendingDefaultName    = defaultName;

            if (!EventBus.HasSubscribers<CraftNameInputPopupRequestEvent>())
            {
                DebugTools.LogWarning("[CraftingUIController] No listener for CraftNameInputPopupRequestEvent. Please add CraftNameInputPopupView.");
                ClearPendingPopupRequest();
                return;
            }

            EventBus.Raise(new CraftNameInputPopupRequestEvent
            {
                RequestId   = _pendingPopupRequestId,
                BlueprintID = blueprintId,
                DefaultName = defaultName
            });
        }

        // --- 事件处理 ---

        private void HandleCraftNamePopupResult(CraftNameInputPopupResultEvent evt)
        {
            if (evt.RequestId != _pendingPopupRequestId) return;

            string blueprintId = _pendingBlueprintId;
            string defaultName = _pendingDefaultName;
            ClearPendingPopupRequest();

            if (!evt.Confirmed || string.IsNullOrWhiteSpace(blueprintId)) return;

            string finalName = string.IsNullOrWhiteSpace(evt.CustomName) ? defaultName : evt.CustomName.Trim();
            CraftingSystem.Instance?.ExecuteCraft(blueprintId, finalName);
        }

        private void HandleInventoryChanged(OnInventoryChanged evt)
        {
            if (!_isVisible || string.IsNullOrWhiteSpace(_listManager.SelectedEntryKey)) return;
            string blueprintId = _listManager.GetSelectedBlueprintId();
            _detailPanel.RefreshRequirementList(blueprintId);
            _detailPanel.RefreshButtonState(blueprintId);
        }

        private void HandleBlueprintConsumed(OnBlueprintConsumed evt)
        {
            if (!_isVisible || _currentTab != CraftTab.Prototype) return;
            if (string.IsNullOrWhiteSpace(evt.BlueprintID)) return;

            string nextKey = _listManager.RemoveByBlueprintId(evt.BlueprintID);
            if (nextKey != null) OnEntrySelected(nextKey);
            else _detailPanel.EnterEmptyState();
        }

        /// <summary>
        /// 制造历史新增事件：
        /// 复现 Tab 下忽略，避免把新记录回灌导致重复 Slot。
        /// </summary>
        private void HandleCraftHistoryRecorded(CraftHistoryRecordedEvent evt)
        {
            if (!_isVisible || _currentTab == CraftTab.Replication) return;
            RebuildCraftList();
            // 制造成功后自动选中末尾（最新记录）
            if (_listManager.EntryOrder.Count > 0)
                OnEntrySelected(_listManager.EntryOrder[_listManager.EntryOrder.Count - 1]);
        }

        private void HandleBlueprintSlotClicked(CraftBlueprintSlotClickedEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible || string.IsNullOrWhiteSpace(evt.EntryKey)) return;
            if (!_listManager.TryGetEntry(evt.EntryKey, out _)) return;
            OnEntrySelected(evt.EntryKey);
        }

        private void HandleOpenCraftingUI(OpenCraftingUIEvent evt)
        {
            if (!isActiveAndEnabled) return;
            RebuildCraftList();
            SetVisible(true);
        }

        private void HandleCloseCraftingUI(CloseCraftingUIEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible) return;
            SetVisible(false);
            _listManager.ReleaseAll();
            _detailPanel.ReleaseAllRequirementSlots();
            _weaponListManager.ReleaseAll();
            _prefixListManager.ReleaseAll();
            _weaponEnhanceDetailPanel.ReleaseAllAppliedPrefixSlots();
            _selectedPrefixWordId = null;
            _rebindIndex = -1;
            ClearPendingPopupRequest();
            ClearPendingRenameRequest();
        }

        private void HandleUICancel()
        {
            if (!isActiveAndEnabled || !_isVisible) return;
            EventBus.Raise(new CloseCraftingUIEvent());
        }

        // --- 强化 Tab：列表重建与选中 ---

        private void RebuildEnhanceTab()
        {
            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            bool hasEntries = _weaponListManager.Rebuild(player);

            _selectedPrefixWordId = null;
            _rebindIndex = -1;

            if (hasEntries)
                OnWeaponSelected(_weaponListManager.EntryOrder[0]);
            else
                _weaponEnhanceDetailPanel.EnterEmptyState();
        }

        private void OnWeaponSelected(string entryKey)
        {
            _weaponListManager.Select(entryKey);
            _selectedPrefixWordId = null;
            _rebindIndex = -1;
            RefreshEnhanceTabDetail();
        }

        /// <summary>
        /// 刷新强化 Tab 右侧全部内容：基础信息、已应用前缀、语料库列表、强化/重铸按钮状态。
        /// </summary>
        private void RefreshEnhanceTabDetail()
        {
            InventorySlot slot = _weaponListManager.GetSelectedSlot();
            if (slot == null)
            {
                _weaponEnhanceDetailPanel.EnterEmptyState();
                return;
            }

            WeaponEnhanceSystem enhanceSystem = WeaponEnhanceSystem.Instance;
            _weaponEnhanceDetailPanel.ShowForWeapon(slot, enhanceSystem, _rebindIndex);

            List<string> appliedIds = slot.WeaponData?.AppliedPrefixWordIds;
            _prefixListManager.Rebuild(enhanceSystem, appliedIds, _selectedPrefixWordId);

            _weaponEnhanceDetailPanel.RefreshEnhanceButtonState(enhanceSystem, slot, _selectedPrefixWordId);
            _weaponEnhanceDetailPanel.RefreshRebindButtonState(enhanceSystem, slot, _rebindIndex, _selectedPrefixWordId);
        }

        /// <summary>
        /// 只刷新语料库高亮与按钮状态，不重建基础信息/已应用前缀列表（选词/选重铸目标时用，避免整面板闪烁）。
        /// </summary>
        private void RefreshEnhanceTabSelectionState()
        {
            InventorySlot slot = _weaponListManager.GetSelectedSlot();
            if (slot == null) return;

            WeaponEnhanceSystem enhanceSystem = WeaponEnhanceSystem.Instance;

            List<string> appliedIds = slot.WeaponData?.AppliedPrefixWordIds;
            _prefixListManager.Rebuild(enhanceSystem, appliedIds, _selectedPrefixWordId);
            _weaponEnhanceDetailPanel.RefreshAppliedPrefixList(slot, enhanceSystem, _rebindIndex);

            _weaponEnhanceDetailPanel.RefreshEnhanceButtonState(enhanceSystem, slot, _selectedPrefixWordId);
            _weaponEnhanceDetailPanel.RefreshRebindButtonState(enhanceSystem, slot, _rebindIndex, _selectedPrefixWordId);
        }

        private void HandleWeaponSlotClicked(WeaponEnhanceSlotClickedEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible || _currentTab != CraftTab.Enhance) return;
            if (string.IsNullOrWhiteSpace(evt.EntryKey)) return;
            OnWeaponSelected(evt.EntryKey);
        }

        private void HandlePrefixSlotClicked(WeaponPrefixSlotClickedEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible || _currentTab != CraftTab.Enhance) return;
            if (string.IsNullOrWhiteSpace(evt.EntryKey)) return;

            _selectedPrefixWordId = evt.EntryKey;
            RefreshEnhanceTabSelectionState();
        }

        private void HandleAppliedPrefixSlotClicked(AppliedPrefixSlotClickedEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible || _currentTab != CraftTab.Enhance) return;

            // 再次点击同一行视为取消重铸目标选择
            _rebindIndex = _rebindIndex == evt.Index ? -1 : evt.Index;
            RefreshEnhanceTabDetail();
        }

        // --- 强化 Tab：强化/重铸操作 ---

        private void HandleEnhanceConfirmClicked()
        {
            InventorySlot slot = _weaponListManager.GetSelectedSlot();
            WeaponEnhanceSystem enhanceSystem = WeaponEnhanceSystem.Instance;
            if (slot == null || enhanceSystem == null || string.IsNullOrWhiteSpace(_selectedPrefixWordId)) return;
            if (!enhanceSystem.CanEnhance(slot, _selectedPrefixWordId, out _)) return;

            string wordId = _selectedPrefixWordId;
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = "确认消耗材料进行强化？",
                OnConfirm = () =>
                {
                    if (!enhanceSystem.ExecuteEnhance(slot, wordId)) return;
                    _selectedPrefixWordId = null;
                    RefreshEnhanceTabDetail();
                },
                OnCancel = null
            });
        }

        private void HandleRebindConfirmClicked()
        {
            InventorySlot slot = _weaponListManager.GetSelectedSlot();
            WeaponEnhanceSystem enhanceSystem = WeaponEnhanceSystem.Instance;
            if (slot == null || enhanceSystem == null || _rebindIndex < 0 || string.IsNullOrWhiteSpace(_selectedPrefixWordId)) return;
            if (!enhanceSystem.CanRebind(slot, _rebindIndex, _selectedPrefixWordId, out _)) return;

            int rebindIndex = _rebindIndex;
            string wordId = _selectedPrefixWordId;
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = "确认消耗材料重铸该前缀？",
                OnConfirm = () =>
                {
                    if (!enhanceSystem.ExecuteRebind(slot, rebindIndex, wordId)) return;
                    _selectedPrefixWordId = null;
                    _rebindIndex = -1;
                    RefreshEnhanceTabDetail();
                },
                OnCancel = null
            });
        }

        private void HandleWeaponEnhanced(WeaponEnhancedEvent evt)
        {
            if (!_isVisible || _currentTab != CraftTab.Enhance) return;
            if (_weaponListManager.GetSelectedSlot() != evt.Slot) return;
            RefreshEnhanceTabDetail();
        }

        private void HandleWeaponRebound(WeaponRebindEvent evt)
        {
            if (!_isVisible || _currentTab != CraftTab.Enhance) return;
            if (_weaponListManager.GetSelectedSlot() != evt.Slot) return;
            RefreshEnhanceTabDetail();
        }

        // --- 改名（背包/强化界面通用） ---

        /// <summary>
        /// 强化详情面板的"改名"按钮：仅对当前选中的武器槽位生效。
        /// </summary>
        private void HandleRenameButtonClicked()
        {
            if (_currentTab != CraftTab.Enhance) return;

            InventorySlot slot = _weaponListManager.GetSelectedSlot();
            if (slot == null) return;

            string defaultName = !string.IsNullOrWhiteSpace(slot.CustomName)
                ? slot.CustomName
                : (slot.Item != null ? slot.Item.GetLocalizedName() : string.Empty);

            _pendingRenameSlot = slot;
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

            InventorySlot slot = _pendingRenameSlot;
            ClearPendingRenameRequest();

            if (!evt.Confirmed || slot == null) return;

            InventoryManager.Instance?.RenameSlot(slot, evt.CustomName);
            RefreshEnhanceTabDetail();
        }

        private void ClearPendingRenameRequest()
        {
            _pendingRenameRequestId = -1;
            _pendingRenameSlot = null;
        }

        // --- 订阅管理 ---

        /// <summary>
        /// EventBusMonoBehaviour 入口：集中注册所有 EventBus 事件。
        /// 反注册由父类在 OnDisable 自动完成。
        /// </summary>
        protected override void Bind()
        {
            Subscribe<OnInventoryChanged>(HandleInventoryChanged);
            Subscribe<OnBlueprintConsumed>(HandleBlueprintConsumed);
            Subscribe<CraftHistoryRecordedEvent>(HandleCraftHistoryRecorded);
            Subscribe<CraftBlueprintSlotClickedEvent>(HandleBlueprintSlotClicked);
            Subscribe<OpenCraftingUIEvent>(HandleOpenCraftingUI);
            Subscribe<CloseCraftingUIEvent>(HandleCloseCraftingUI);
            Subscribe<CraftNameInputPopupResultEvent>(HandleCraftNamePopupResult);
            Subscribe<RenameSlotPopupResultEvent>(HandleRenamePopupResult);
            Subscribe<WeaponEnhanceSlotClickedEvent>(HandleWeaponSlotClicked);
            Subscribe<WeaponPrefixSlotClickedEvent>(HandlePrefixSlotClicked);
            Subscribe<AppliedPrefixSlotClickedEvent>(HandleAppliedPrefixSlotClicked);
            Subscribe<WeaponEnhancedEvent>(HandleWeaponEnhanced);
            Subscribe<WeaponRebindEvent>(HandleWeaponRebound);
        }

        private void SubscribeInput()
        {
            if (inputReader != null) inputReader.UICancelEvent += HandleUICancel;
        }

        private void UnsubscribeInput()
        {
            if (inputReader != null) inputReader.UICancelEvent -= HandleUICancel;
        }

        // --- 工具方法 ---

        private void ClearPendingPopupRequest()
        {
            _pendingPopupRequestId = -1;
            _pendingBlueprintId    = string.Empty;
            _pendingDefaultName    = string.Empty;
        }

        private void EnsureCanvasGroup()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// 软显隐：保持 GameObject 激活以持续监听 EventBus。
        /// </summary>
        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup == null) EnsureCanvasGroup();
            if (_canvasGroup == null) return;
            _canvasGroup.alpha         = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable   = visible;
        }
    }
}
