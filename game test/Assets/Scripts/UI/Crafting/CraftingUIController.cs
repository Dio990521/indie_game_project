using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 打造界面控制器（核心 UI 逻辑层）：
    ///
    /// 设计目标：
    /// 1) 通过 EventBus 驱动界面开关与交互，避免 UI 与业务系统直接耦合。
    /// 2) 在同一套“左侧列表 + 右侧详情”布局上，复用实现两个 Tab：
    ///    - 原型制造（Prototype）
    ///    - 复现制造（Replication）
    /// 3) 点击制造后先弹“自定义名称输入弹窗”，确认后才真正执行制造。
    ///
    /// 架构边界：
    /// - CraftUIBinder：只保存引用，不写逻辑。
    /// - CraftingUIController：处理交互、列表、Tab、弹窗流程。
    /// - CraftingSystem：只负责规则与数据（扣料、产出、历史、存档）。
    /// </summary>
    public class CraftingUIController : MonoBehaviour
    {
        /// <summary>
        /// Tab 类型定义：
        /// Prototype：原型制造，显示尚未消耗的蓝图条目。
        /// Replication：复现制造，显示制造历史条目（名称使用玩家自定义名）。
        /// </summary>
        private enum CraftTab
        {
            Prototype,
            Replication
        }

        /// <summary>
        /// 左侧列表运行时条目模型：
        /// 该结构体是 UI 适配层，不直接落盘，仅用于把不同数据源统一映射到同一 UI 组件。
        /// </summary>
        private struct CraftListEntry
        {
            // 列表条目唯一键（必须唯一；用于点击后精确定位）
            public string EntryKey;
            // 对应蓝图 ID（用于右侧配方查询与制造执行）
            public string BlueprintID;
            // 左侧列表显示名称
            public string DisplayName;
            // 本条目在“弹窗默认名称”场景下建议使用的默认值
            public string SuggestedName;
        }

        [Header("References")]
        [SerializeField] private CraftUIBinder binder;
        [SerializeField] private GameInputReader inputReader;

        [Header("Pool Settings")]
        [Tooltip("左侧图纸列表对象池预热数量")]
        [SerializeField] private int slotPoolWarmup = 8;
        [Tooltip("右侧材料列表对象池预热数量")]
        [SerializeField] private int requirementPoolWarmup = 10;

        // 对象池：减少频繁 Instantiate/Destroy 带来的 GC 与 CPU 抖动
        private GameObjectPool _slotPool;
        private GameObjectPool _requirementPool;

        // 当前激活的 UI 实例缓存（用于回收）
        private readonly List<BlueprintSlotUI> _activeBlueprintSlots = new List<BlueprintSlotUI>();
        private readonly List<RequirementSlotUI> _activeRequirementSlots = new List<RequirementSlotUI>();

        // 左侧条目索引：
        // EntryKey -> 列表项 UI
        private readonly Dictionary<string, BlueprintSlotUI> _slotByEntryKey = new Dictionary<string, BlueprintSlotUI>(StringComparer.Ordinal);
        // EntryKey -> 列表数据
        private readonly Dictionary<string, CraftListEntry> _entryByKey = new Dictionary<string, CraftListEntry>(StringComparer.Ordinal);
        // 列表顺序（用于自动选中第 0 项、删除后回退等）
        private readonly List<string> _entryOrder = new List<string>();

        // 临时缓存（避免每次刷新都 new）
        private readonly List<BlueprintRecord> _availableRecordsCache = new List<BlueprintRecord>();
        private readonly List<CraftHistoryEntry> _historyCache = new List<CraftHistoryEntry>();
        private readonly Dictionary<ItemSO, int> _inventoryCountCache = new Dictionary<ItemSO, int>();

        // 当前 UI 状态
        private CraftTab _currentTab = CraftTab.Prototype;
        private string _selectedEntryKey;
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
                Debug.LogError("[CraftingUIController] Missing CraftUIBinder reference.");
                return;
            }

            EnsurePools();
            EnsureCanvasGroup();

            if (binder.CraftButton != null)
            {
                binder.CraftButton.onClick.AddListener(HandleCraftButtonClicked);
            }
            if (binder.PrototypeTabButton != null)
            {
                binder.PrototypeTabButton.onClick.AddListener(HandlePrototypeTabClicked);
            }
            if (binder.ReplicationTabButton != null)
            {
                binder.ReplicationTabButton.onClick.AddListener(HandleReplicationTabClicked);
            }
        }

        private void OnDestroy()
        {
            if (binder != null)
            {
                if (binder.CraftButton != null) binder.CraftButton.onClick.RemoveListener(HandleCraftButtonClicked);
                if (binder.PrototypeTabButton != null) binder.PrototypeTabButton.onClick.RemoveListener(HandlePrototypeTabClicked);
                if (binder.ReplicationTabButton != null) binder.ReplicationTabButton.onClick.RemoveListener(HandleReplicationTabClicked);
            }
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

            // 禁用时释放所有动态 UI，防止下次启用出现残留状态
            ReleaseAllBlueprintSlots();
            ReleaseAllRequirementSlots();
            _selectedEntryKey = null;
            ClearPendingPopupRequest();
        }

        /// <summary>
        /// 原型 Tab 按钮回调。
        /// </summary>
        private void HandlePrototypeTabClicked()
        {
            SwitchTab(CraftTab.Prototype);
        }

        /// <summary>
        /// 复现 Tab 按钮回调。
        /// </summary>
        private void HandleReplicationTabClicked()
        {
            SwitchTab(CraftTab.Replication);
        }

        /// <summary>
        /// 切换 Tab 并刷新左侧列表。
        /// </summary>
        private void SwitchTab(CraftTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            if (!_isVisible) return;
            RebuildCraftList();
        }

        /// <summary>
        /// 重建左侧列表：
        /// - Prototype：数据源为“未消耗蓝图记录”
        /// - Replication：数据源为“制造历史记录”
        ///
        /// 两者共用同一个 Slot 预制体和同一套右侧详情面板。
        /// </summary>
        private void RebuildCraftList()
        {
            ReleaseAllBlueprintSlots();
            _selectedEntryKey = null;

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null)
            {
                EnterEmptyState();
                return;
            }

            if (_currentTab == CraftTab.Prototype)
            {
                BuildPrototypeList(craftingSystem);
            }
            else
            {
                BuildReplicationList(craftingSystem);
            }

            // 自动选中逻辑：有条目选第 0 条；无条目进入空态
            if (_entryOrder.Count > 0)
            {
                OnEntrySelected(_entryOrder[0]);
            }
            else
            {
                EnterEmptyState();
            }
        }

        /// <summary>
        /// 构建“原型制造”列表。
        /// 规则：显示产出物原始名称（不是自定义名）。
        /// </summary>
        private void BuildPrototypeList(CraftingSystem craftingSystem)
        {
            craftingSystem.GetAvailableBlueprintRecords(_availableRecordsCache);

            for (int i = 0; i < _availableRecordsCache.Count; i++)
            {
                BlueprintRecord record = _availableRecordsCache[i];
                if (record == null || string.IsNullOrWhiteSpace(record.ID)) continue;

                BlueprintSO blueprint = craftingSystem.GetBlueprint(record.ID);
                if (blueprint == null) continue;

                string entryKey = $"P:{record.ID}";
                string productOriginalName = craftingSystem.GetOriginalProductName(blueprint);
                string displayName = string.IsNullOrWhiteSpace(productOriginalName) ? blueprint.DefaultName : productOriginalName;

                CraftListEntry entry = new CraftListEntry
                {
                    EntryKey = entryKey,
                    BlueprintID = blueprint.ID,
                    DisplayName = displayName,
                    SuggestedName = displayName
                };

                AddListEntry(entry, blueprint.GetDisplayIcon());
            }
        }

        /// <summary>
        /// 构建“复现制造”列表。
        /// 规则：显示历史中的最终名称（玩家自定义名）。
        /// </summary>
        private void BuildReplicationList(CraftingSystem craftingSystem)
        {
            craftingSystem.GetCraftHistory(_historyCache);
            // 复现列表去重集合：
            // 目的：CraftingSystem 历史会记录“每一次制造行为”，其中可能出现完全相同的
            //      BlueprintID + CustomName 组合。这里用 HashSet 做一次 UI 层去重，
            //      保证左侧仅展示“不重复的配方+命名组合”。
            // 说明：键格式固定为 "BlueprintID|CustomName"（按需求定义）。
            HashSet<string> dedupeKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _historyCache.Count; i++)
            {
                CraftHistoryEntry history = _historyCache[i];
                if (history == null || string.IsNullOrWhiteSpace(history.BlueprintID)) continue;

                // 自定义名用于去重时做 Trim 归一化，避免仅因首尾空格不同而被视为不同条目。
                string normalizedCustomName = string.IsNullOrWhiteSpace(history.CustomName)
                    ? string.Empty
                    : history.CustomName.Trim();
                string dedupeKey = history.BlueprintID + "|" + normalizedCustomName;
                if (!dedupeKeys.Add(dedupeKey))
                {
                    // 已存在同组合，直接跳过，不再生成重复 Slot。
                    continue;
                }

                BlueprintSO blueprint = craftingSystem.GetBlueprint(history.BlueprintID);
                if (blueprint == null) continue;

                string entryKey = $"R:{i}";
                string fallbackName = craftingSystem.GetOriginalProductName(blueprint);
                string displayName = string.IsNullOrWhiteSpace(normalizedCustomName) ? fallbackName : normalizedCustomName;

                CraftListEntry entry = new CraftListEntry
                {
                    EntryKey = entryKey,
                    BlueprintID = blueprint.ID,
                    DisplayName = displayName,
                    SuggestedName = displayName
                };

                AddListEntry(entry, blueprint.GetDisplayIcon());
            }
        }

        /// <summary>
        /// 添加单条列表项到 UI 与索引。
        /// </summary>
        private void AddListEntry(CraftListEntry entry, Sprite icon)
        {
            BlueprintSlotUI slotUI = SpawnBlueprintSlot(entry, icon);
            if (slotUI == null) return;

            _activeBlueprintSlots.Add(slotUI);
            _slotByEntryKey[entry.EntryKey] = slotUI;
            _entryByKey[entry.EntryKey] = entry;
            _entryOrder.Add(entry.EntryKey);
        }

        /// <summary>
        /// 选中列表条目后刷新右侧详情。
        /// </summary>
        private void OnEntrySelected(string entryKey)
        {
            if (!_entryByKey.TryGetValue(entryKey, out CraftListEntry entry))
            {
                EnterEmptyState();
                return;
            }

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null)
            {
                EnterEmptyState();
                return;
            }

            BlueprintSO blueprint = craftingSystem.GetBlueprint(entry.BlueprintID);
            if (blueprint == null)
            {
                EnterEmptyState();
                return;
            }

            _selectedEntryKey = entryKey;

            if (binder.EmptyStateNode != null)
            {
                binder.EmptyStateNode.SetActive(false);
            }

            SetDetailPanelVisible(true);

            // 右侧按需求只显示成品图标，不显示成品名称文本
            if (binder.ProductIcon != null)
            {
                binder.ProductIcon.sprite = blueprint.GetDisplayIcon();
                binder.ProductIcon.enabled = binder.ProductIcon.sprite != null;
            }

            RefreshRequirementList();
            RefreshButtonState();
        }

        /// <summary>
        /// 刷新右侧材料列表（共享逻辑，两个 Tab 通用）。
        /// </summary>
        private void RefreshRequirementList()
        {
            ReleaseAllRequirementSlots();

            string blueprintId = GetSelectedBlueprintId();
            if (string.IsNullOrWhiteSpace(blueprintId)) return;

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null) return;

            BlueprintSO blueprint = craftingSystem.GetBlueprint(blueprintId);
            if (blueprint == null) return;
            if (binder.RequirementsRoot == null) return;

            RebuildInventoryCountCache();

            IReadOnlyList<BlueprintRequirement> requirements = blueprint.Requirements;
            for (int i = 0; i < requirements.Count; i++)
            {
                BlueprintRequirement requirement = requirements[i];
                RequirementSlotUI requirementUI = SpawnRequirementSlot();
                if (requirementUI == null) continue;

                int ownedCount = 0;
                if (requirement != null && requirement.Item != null)
                {
                    _inventoryCountCache.TryGetValue(requirement.Item, out ownedCount);
                }

                requirementUI.Setup(requirement, ownedCount);
                _activeRequirementSlots.Add(requirementUI);
            }
        }

        /// <summary>
        /// 刷新制造按钮可点击状态（共享逻辑，两个 Tab 通用）。
        /// </summary>
        private void RefreshButtonState()
        {
            if (binder.CraftButton == null) return;

            string blueprintId = GetSelectedBlueprintId();
            if (string.IsNullOrWhiteSpace(blueprintId))
            {
                binder.CraftButton.interactable = false;
                return;
            }

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            bool canCraft = craftingSystem != null && craftingSystem.CanCraft(blueprintId);
            binder.CraftButton.interactable = canCraft;
        }

        /// <summary>
        /// 制造按钮点击逻辑：
        /// - Prototype（原型制造）：先请求输入弹窗，确认后再制造。
        /// - Replication（复现制造）：跳过弹窗，直接用历史条目的 SuggestedName 执行制造。
        /// </summary>
        private void HandleCraftButtonClicked()
        {
            if (!_isVisible) return;
            if (_pendingPopupRequestId >= 0) return;

            string blueprintId = GetSelectedBlueprintId();
            if (string.IsNullOrWhiteSpace(blueprintId)) return;

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null) return;

            // 复现制造分支：
            // 直接读取当前选中条目里的 SuggestedName（历史中记录的名称），
            // 不再弹输入框，立即执行制造。
            if (_currentTab == CraftTab.Replication)
            {
                string replicationName = GetSelectedSuggestedName();
                if (string.IsNullOrWhiteSpace(replicationName))
                {
                    // 理论上复现条目应始终有名称，这里做兜底防御，
                    // 若异常为空则回退成品原始名称，避免传入空名。
                    BlueprintSO fallbackBlueprint = craftingSystem.GetBlueprint(blueprintId);
                    if (fallbackBlueprint == null) return;
                    replicationName = craftingSystem.GetOriginalProductName(fallbackBlueprint);
                }

                craftingSystem.ExecuteCraft(blueprintId, replicationName);
                return;
            }

            BlueprintSO blueprint = craftingSystem.GetBlueprint(blueprintId);
            if (blueprint == null) return;

            // 弹窗默认文本：
            // - 原型 Tab：产出物原始名
            // - 复现 Tab：当前历史条目的自定义名（或回退原始名）
            string defaultName = GetSelectedSuggestedName();
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = craftingSystem.GetOriginalProductName(blueprint);
            }

            // 记录请求上下文，等待弹窗响应事件
            _pendingPopupRequestId = ++_popupRequestSeed;
            _pendingBlueprintId = blueprintId;
            _pendingDefaultName = defaultName;

            // 若当前没有任何输入弹窗监听该事件，则给出明确警告并取消本次请求，
            // 避免玩家点击制造后无反馈。
            if (!EventBus.HasSubscribers<CraftNameInputPopupRequestEvent>())
            {
                Debug.LogWarning("[CraftingUIController] No listener for CraftNameInputPopupRequestEvent. Please add CraftNameInputPopupView.");
                ClearPendingPopupRequest();
                return;
            }

            EventBus.Raise(new CraftNameInputPopupRequestEvent
            {
                RequestId = _pendingPopupRequestId,
                BlueprintID = blueprintId,
                DefaultName = defaultName
            });
        }

        /// <summary>
        /// 输入弹窗响应处理：
        /// - 取消：直接结束，不扣材料
        /// - 确认：执行制造，并把输入名称传给系统
        /// </summary>
        private void HandleCraftNamePopupResult(CraftNameInputPopupResultEvent evt)
        {
            // 只处理当前待确认请求，其他响应一律忽略
            if (evt.RequestId != _pendingPopupRequestId) return;

            string blueprintId = _pendingBlueprintId;
            string defaultName = _pendingDefaultName;
            ClearPendingPopupRequest();

            if (!evt.Confirmed) return;
            if (string.IsNullOrWhiteSpace(blueprintId)) return;

            string finalName = string.IsNullOrWhiteSpace(evt.CustomName)
                ? defaultName
                : evt.CustomName.Trim();

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null) return;
            craftingSystem.ExecuteCraft(blueprintId, finalName);
        }

        /// <summary>
        /// 背包变化时刷新右侧需求与按钮。
        /// </summary>
        private void HandleInventoryChanged(OnInventoryChanged evt)
        {
            if (!_isVisible) return;
            if (string.IsNullOrWhiteSpace(_selectedEntryKey)) return;
            RefreshRequirementList();
            RefreshButtonState();
        }

        /// <summary>
        /// 图纸首次消耗事件：
        /// 仅在原型 Tab 需要实时移除对应条目。
        /// </summary>
        private void HandleBlueprintConsumed(OnBlueprintConsumed evt)
        {
            if (!_isVisible) return;
            if (_currentTab != CraftTab.Prototype) return;
            if (string.IsNullOrWhiteSpace(evt.BlueprintID)) return;

            // 收集所有对应蓝图的条目键（理论上原型列表只会有 1 条，但此处写成通用逻辑）
            List<string> toRemove = null;
            for (int i = 0; i < _entryOrder.Count; i++)
            {
                string key = _entryOrder[i];
                if (!_entryByKey.TryGetValue(key, out CraftListEntry entry)) continue;
                if (!string.Equals(entry.BlueprintID, evt.BlueprintID, StringComparison.Ordinal)) continue;
                if (toRemove == null) toRemove = new List<string>();
                toRemove.Add(key);
            }

            if (toRemove == null || toRemove.Count == 0) return;

            for (int i = 0; i < toRemove.Count; i++)
            {
                RemoveEntryByKey(toRemove[i]);
            }

            if (_entryOrder.Count > 0)
            {
                OnEntrySelected(_entryOrder[0]);
            }
            else
            {
                EnterEmptyState();
            }
        }

        /// <summary>
        /// 制造历史新增事件：
        /// 重要约束：在复现 Tab 下直接忽略该事件，避免“复现制造成功后”把新记录再次
        /// 回灌到左侧列表导致重复 Slot。
        /// </summary>
        private void HandleCraftHistoryRecorded(CraftHistoryRecordedEvent evt)
        {
            if (!_isVisible) return;
            if (_currentTab == CraftTab.Replication) return;

            RebuildCraftList();
            // 新记录追加在末尾，刷新后默认选中末尾更符合“刚刚制造成功”的反馈预期
            if (_entryOrder.Count > 0)
            {
                OnEntrySelected(_entryOrder[_entryOrder.Count - 1]);
            }
        }

        /// <summary>
        /// 左侧槽位点击事件处理。
        /// </summary>
        private void HandleBlueprintSlotClicked(CraftBlueprintSlotClickedEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible) return;
            if (string.IsNullOrWhiteSpace(evt.EntryKey)) return;
            if (!_entryByKey.ContainsKey(evt.EntryKey)) return;
            OnEntrySelected(evt.EntryKey);
        }

        /// <summary>
        /// 打开 Craft UI 事件：
        /// 由外部入口（如 Camp 动作按钮）触发。
        /// </summary>
        private void HandleOpenCraftingUI(OpenCraftingUIEvent evt)
        {
            if (!isActiveAndEnabled) return;
            RebuildCraftList();
            SetVisible(true);
        }

        /// <summary>
        /// 关闭 Craft UI 事件：
        /// 统一隐藏并清理运行时状态。
        /// </summary>
        private void HandleCloseCraftingUI(CloseCraftingUIEvent evt)
        {
            if (!isActiveAndEnabled) return;
            if (!_isVisible) return;

            SetVisible(false);
            ReleaseAllBlueprintSlots();
            ReleaseAllRequirementSlots();
            _selectedEntryKey = null;
            ClearPendingPopupRequest();
        }

        /// <summary>
        /// ESC / Cancel 输入处理：
        /// 不直接改 UI，统一转发为 CloseCraftingUIEvent。
        /// </summary>
        private void HandleUICancel()
        {
            if (!isActiveAndEnabled || !_isVisible) return;
            EventBus.Raise(new CloseCraftingUIEvent());
        }

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

        /// <summary>
        /// 输入订阅安全：
        /// 必须在 OnEnable/OnDisable 成对订阅/注销。
        /// </summary>
        private void SubscribeInput()
        {
            if (inputReader == null) return;
            inputReader.UICancelEvent += HandleUICancel;
        }

        private void UnsubscribeInput()
        {
            if (inputReader == null) return;
            inputReader.UICancelEvent -= HandleUICancel;
        }

        /// <summary>
        /// 创建左侧条目 UI（对象池）。
        /// </summary>
        private BlueprintSlotUI SpawnBlueprintSlot(CraftListEntry entry, Sprite icon)
        {
            if (_slotPool == null || binder.ListRoot == null) return null;

            GameObject go = _slotPool.Get();
            go.transform.SetParent(binder.ListRoot, false);

            BlueprintSlotUI slotUI = go.GetComponent<BlueprintSlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("[CraftingUIController] slotPrefab missing BlueprintSlotUI component.");
                _slotPool.Release(go);
                return null;
            }

            slotUI.Setup(entry.EntryKey, entry.BlueprintID, icon, entry.DisplayName);
            return slotUI;
        }

        /// <summary>
        /// 创建右侧材料条目 UI（对象池）。
        /// </summary>
        private RequirementSlotUI SpawnRequirementSlot()
        {
            if (_requirementPool == null || binder.RequirementsRoot == null) return null;

            GameObject go = _requirementPool.Get();
            go.transform.SetParent(binder.RequirementsRoot, false);

            RequirementSlotUI requirementUI = go.GetComponent<RequirementSlotUI>();
            if (requirementUI == null)
            {
                Debug.LogError("[CraftingUIController] requirementSlotPrefab missing RequirementSlotUI component.");
                _requirementPool.Release(go);
                return null;
            }

            return requirementUI;
        }

        /// <summary>
        /// 按条目键移除左侧条目并回收对象池。
        /// </summary>
        private void RemoveEntryByKey(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey)) return;

            if (_slotByEntryKey.TryGetValue(entryKey, out BlueprintSlotUI slotUI))
            {
                if (slotUI != null && _slotPool != null)
                {
                    _slotPool.Release(slotUI.gameObject);
                }
                _activeBlueprintSlots.Remove(slotUI);
            }

            _slotByEntryKey.Remove(entryKey);
            _entryByKey.Remove(entryKey);
            _entryOrder.Remove(entryKey);
        }

        /// <summary>
        /// 回收所有左侧条目。
        /// </summary>
        private void ReleaseAllBlueprintSlots()
        {
            for (int i = 0; i < _activeBlueprintSlots.Count; i++)
            {
                BlueprintSlotUI slot = _activeBlueprintSlots[i];
                if (slot == null) continue;
                if (_slotPool != null)
                {
                    _slotPool.Release(slot.gameObject);
                }
            }

            _activeBlueprintSlots.Clear();
            _slotByEntryKey.Clear();
            _entryByKey.Clear();
            _entryOrder.Clear();
        }

        /// <summary>
        /// 回收所有右侧材料条目。
        /// </summary>
        private void ReleaseAllRequirementSlots()
        {
            for (int i = 0; i < _activeRequirementSlots.Count; i++)
            {
                RequirementSlotUI requirementSlot = _activeRequirementSlots[i];
                if (requirementSlot == null) continue;
                if (_requirementPool != null)
                {
                    _requirementPool.Release(requirementSlot.gameObject);
                }
            }

            _activeRequirementSlots.Clear();
        }

        /// <summary>
        /// 重建背包数量缓存（用于右侧需求显示：拥有/需求）。
        /// </summary>
        private void RebuildInventoryCountCache()
        {
            _inventoryCountCache.Clear();

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null || inventory.slots == null) return;

            for (int i = 0; i < inventory.slots.Count; i++)
            {
                InventorySlot slot = inventory.slots[i];
                if (slot == null || slot.Item == null || slot.Count <= 0) continue;

                if (_inventoryCountCache.TryGetValue(slot.Item, out int current))
                {
                    _inventoryCountCache[slot.Item] = current + slot.Count;
                }
                else
                {
                    _inventoryCountCache[slot.Item] = slot.Count;
                }
            }
        }

        /// <summary>
        /// 进入空状态：
        /// - 显示 emptyStateNode
        /// - 隐藏右侧图标、材料、制造按钮
        /// </summary>
        private void EnterEmptyState()
        {
            _selectedEntryKey = null;

            if (binder.EmptyStateNode != null)
            {
                binder.EmptyStateNode.SetActive(true);
            }

            SetDetailPanelVisible(false);

            if (binder.ProductIcon != null)
            {
                binder.ProductIcon.sprite = null;
                binder.ProductIcon.enabled = false;
            }

            ReleaseAllRequirementSlots();

            if (binder.CraftButton != null)
            {
                binder.CraftButton.interactable = false;
            }
        }

        /// <summary>
        /// 统一切换右侧详情可见性。
        /// </summary>
        private void SetDetailPanelVisible(bool visible)
        {
            if (binder.ProductIcon != null)
            {
                binder.ProductIcon.gameObject.SetActive(visible);
            }
            if (binder.RequirementsRoot != null)
            {
                binder.RequirementsRoot.gameObject.SetActive(visible);
            }
            if (binder.CraftButton != null)
            {
                binder.CraftButton.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 获取当前选中条目对应的蓝图 ID。
        /// </summary>
        private string GetSelectedBlueprintId()
        {
            if (string.IsNullOrWhiteSpace(_selectedEntryKey)) return string.Empty;
            return _entryByKey.TryGetValue(_selectedEntryKey, out CraftListEntry entry)
                ? entry.BlueprintID
                : string.Empty;
        }

        /// <summary>
        /// 获取当前选中条目的“建议默认名称”。
        /// </summary>
        private string GetSelectedSuggestedName()
        {
            if (string.IsNullOrWhiteSpace(_selectedEntryKey)) return string.Empty;
            return _entryByKey.TryGetValue(_selectedEntryKey, out CraftListEntry entry)
                ? entry.SuggestedName
                : string.Empty;
        }

        /// <summary>
        /// 清空弹窗待确认上下文。
        /// </summary>
        private void ClearPendingPopupRequest()
        {
            _pendingPopupRequestId = -1;
            _pendingBlueprintId = string.Empty;
            _pendingDefaultName = string.Empty;
        }

        /// <summary>
        /// 初始化对象池。
        /// </summary>
        private void EnsurePools()
        {
            if (binder == null) return;

            if (_slotPool == null && binder.SlotPrefab != null && binder.ListRoot != null)
            {
                _slotPool = new GameObjectPool(binder.SlotPrefab, binder.ListRoot, slotPoolWarmup);
            }

            if (_requirementPool == null && binder.RequirementSlotPrefab != null && binder.RequirementsRoot != null)
            {
                _requirementPool = new GameObjectPool(binder.RequirementSlotPrefab, binder.RequirementsRoot, requirementPoolWarmup);
            }
        }

        /// <summary>
        /// 确保存在 CanvasGroup：
        /// 使用软显隐保持控制器始终激活，以持续监听 EventBus。
        /// </summary>
        private void EnsureCanvasGroup()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 软显隐：
        /// visible=true  时：显示并可交互
        /// visible=false 时：隐藏且不可交互
        /// </summary>
        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup == null) EnsureCanvasGroup();
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
