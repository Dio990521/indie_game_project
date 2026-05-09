using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;

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
    /// - CraftingUIController（本类）：协调上述两者，处理生命周期与事件路由。
    /// - CraftingSystem：只负责规则与数据（扣料、产出、历史、存档）。
    /// </summary>
    public class CraftingUIController : MonoBehaviour
    {
        /// <summary>
        /// Tab 类型：Prototype 显示未消耗蓝图，Replication 显示历史记录。
        /// </summary>
        private enum CraftTab { Prototype, Replication }

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

        private CraftTab _currentTab = CraftTab.Prototype;
        private bool _isVisible;
        private CanvasGroup _canvasGroup;

        // 弹窗请求上下文（通过 RequestId 匹配响应，避免多次点击串线）
        private int _popupRequestSeed;
        private int _pendingPopupRequestId = -1;
        private string _pendingBlueprintId;
        private string _pendingDefaultName;

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

            EnsureCanvasGroup();

            if (binder.CraftButton != null) binder.CraftButton.onClick.AddListener(HandleCraftButtonClicked);
            if (binder.PrototypeTabButton != null) binder.PrototypeTabButton.onClick.AddListener(HandlePrototypeTabClicked);
            if (binder.ReplicationTabButton != null) binder.ReplicationTabButton.onClick.AddListener(HandleReplicationTabClicked);
        }

        private void OnDestroy()
        {
            if (binder == null) return;
            if (binder.CraftButton != null) binder.CraftButton.onClick.RemoveListener(HandleCraftButtonClicked);
            if (binder.PrototypeTabButton != null) binder.PrototypeTabButton.onClick.RemoveListener(HandlePrototypeTabClicked);
            if (binder.ReplicationTabButton != null) binder.ReplicationTabButton.onClick.RemoveListener(HandleReplicationTabClicked);
        }

        private void OnEnable()
        {
            SubscribeEvents();
            SubscribeInput();
            // 重要：默认隐藏，等待 OpenCraftingUIEvent 再显示
            SetVisible(false);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            UnsubscribeInput();
            _listManager.ReleaseAll();
            _detailPanel.ReleaseAllRequirementSlots();
            ClearPendingPopupRequest();
        }

        // --- Tab 切换 ---

        private void HandlePrototypeTabClicked() => SwitchTab(CraftTab.Prototype);
        private void HandleReplicationTabClicked() => SwitchTab(CraftTab.Replication);

        private void SwitchTab(CraftTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            if (_isVisible) RebuildCraftList();
        }

        // --- 列表重建 ---

        private void RebuildCraftList()
        {
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
            ClearPendingPopupRequest();
        }

        private void HandleUICancel()
        {
            if (!isActiveAndEnabled || !_isVisible) return;
            EventBus.Raise(new CloseCraftingUIEvent());
        }

        // --- 订阅管理 ---

        private void SubscribeEvents()
        {
            EventBus.Subscribe<OnInventoryChanged>(HandleInventoryChanged);
            EventBus.Subscribe<OnBlueprintConsumed>(HandleBlueprintConsumed);
            EventBus.Subscribe<CraftHistoryRecordedEvent>(HandleCraftHistoryRecorded);
            EventBus.Subscribe<CraftBlueprintSlotClickedEvent>(HandleBlueprintSlotClicked);
            EventBus.Subscribe<OpenCraftingUIEvent>(HandleOpenCraftingUI);
            EventBus.Subscribe<CloseCraftingUIEvent>(HandleCloseCraftingUI);
            EventBus.Subscribe<CraftNameInputPopupResultEvent>(HandleCraftNamePopupResult);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<OnInventoryChanged>(HandleInventoryChanged);
            EventBus.Unsubscribe<OnBlueprintConsumed>(HandleBlueprintConsumed);
            EventBus.Unsubscribe<CraftHistoryRecordedEvent>(HandleCraftHistoryRecorded);
            EventBus.Unsubscribe<CraftBlueprintSlotClickedEvent>(HandleBlueprintSlotClicked);
            EventBus.Unsubscribe<OpenCraftingUIEvent>(HandleOpenCraftingUI);
            EventBus.Unsubscribe<CloseCraftingUIEvent>(HandleCloseCraftingUI);
            EventBus.Unsubscribe<CraftNameInputPopupResultEvent>(HandleCraftNamePopupResult);
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
