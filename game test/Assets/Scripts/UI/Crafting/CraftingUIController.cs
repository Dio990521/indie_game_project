using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Equipment;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 打造界面协调器（薄控制层）：
    /// 负责界面显隐、大类/列表模式/装备部位筛选切换、弹窗流程、EventBus订阅与输入处理。
    ///
    /// 架构边界：
    /// - CraftUIBinder：只保存引用，不写逻辑。
    /// - CraftingListManager：管理左侧列表对象池、条目索引、构建逻辑。
    /// - CraftingDetailPanel：管理右侧详情面板、需求槽位池、打造效果预览、按钮状态。
    /// - CraftingUIController（本类）：协调上述两者，处理生命周期与事件路由。
    /// - CraftingSystem：只负责规则与数据（扣料、产出、历史、存档）。
    /// </summary>
    public class CraftingUIController : EventBusMonoBehaviour
    {
        /// <summary>大类：装备 / 合成（按 ProductItem.Category 区分）。</summary>
        private enum CraftCategory { Equipment, Synthesis }

        /// <summary>列表模式：未打造（一次性图纸）/ 已打造（可重复复现）。</summary>
        private enum CraftListMode { Blueprint, Crafted }

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

        private CraftCategory _currentCategory = CraftCategory.Equipment;
        private CraftListMode _currentListMode = CraftListMode.Blueprint;
        // 装备大类默认落在"武器图纸"分类（与 SwitchCategory 切入装备大类时的默认值保持一致）
        private EquipmentType? _currentSubFilter = EquipmentType.Weapon;

        private bool _isVisible;
        private CanvasGroup _canvasGroup;

        // 弹窗请求上下文（通过 RequestId 匹配响应，避免多次点击串线）
        private int _popupRequestSeed;
        private int _pendingPopupRequestId = -1;
        private string _pendingBlueprintId;
        private string _pendingDefaultName;
        // ESC/手柄 Cancel 关闭绑定
        private EscCloseBinding _escBinding;

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

            _escBinding = new EscCloseBinding(inputReader, () => _isVisible, () => EventBus.Raise(new CloseCraftingUIEvent()));

            ApplyCategorySpecificControlsVisibility();

            if (binder.CraftButton != null) binder.CraftButton.onClick.AddListener(HandleCraftButtonClicked);
            if (binder.EquipmentCategoryButton != null) binder.EquipmentCategoryButton.onClick.AddListener(HandleEquipmentCategoryClicked);
            if (binder.SynthesisCategoryButton != null) binder.SynthesisCategoryButton.onClick.AddListener(HandleSynthesisCategoryClicked);
            if (binder.BlueprintListModeButton != null) binder.BlueprintListModeButton.onClick.AddListener(HandleBlueprintListModeClicked);
            if (binder.CraftedListModeButton != null) binder.CraftedListModeButton.onClick.AddListener(HandleCraftedListModeClicked);
            if (binder.WeaponBlueprintTabButton != null) binder.WeaponBlueprintTabButton.onClick.AddListener(HandleWeaponBlueprintTabClicked);
            if (binder.ArmorBlueprintTabButton != null) binder.ArmorBlueprintTabButton.onClick.AddListener(HandleArmorBlueprintTabClicked);
            if (binder.WeaponCraftedTabButton != null) binder.WeaponCraftedTabButton.onClick.AddListener(HandleWeaponCraftedTabClicked);
            if (binder.ArmorCraftedTabButton != null) binder.ArmorCraftedTabButton.onClick.AddListener(HandleArmorCraftedTabClicked);
        }

        private void OnDestroy()
        {
            if (binder == null) return;
            if (binder.CraftButton != null) binder.CraftButton.onClick.RemoveListener(HandleCraftButtonClicked);
            if (binder.EquipmentCategoryButton != null) binder.EquipmentCategoryButton.onClick.RemoveListener(HandleEquipmentCategoryClicked);
            if (binder.SynthesisCategoryButton != null) binder.SynthesisCategoryButton.onClick.RemoveListener(HandleSynthesisCategoryClicked);
            if (binder.BlueprintListModeButton != null) binder.BlueprintListModeButton.onClick.RemoveListener(HandleBlueprintListModeClicked);
            if (binder.CraftedListModeButton != null) binder.CraftedListModeButton.onClick.RemoveListener(HandleCraftedListModeClicked);
            if (binder.WeaponBlueprintTabButton != null) binder.WeaponBlueprintTabButton.onClick.RemoveListener(HandleWeaponBlueprintTabClicked);
            if (binder.ArmorBlueprintTabButton != null) binder.ArmorBlueprintTabButton.onClick.RemoveListener(HandleArmorBlueprintTabClicked);
            if (binder.WeaponCraftedTabButton != null) binder.WeaponCraftedTabButton.onClick.RemoveListener(HandleWeaponCraftedTabClicked);
            if (binder.ArmorCraftedTabButton != null) binder.ArmorCraftedTabButton.onClick.RemoveListener(HandleArmorCraftedTabClicked);
        }

        protected override void OnEnable()
        {
            // 父类调用 Bind() 自动订阅所有 EventBus 事件
            base.OnEnable();
            _escBinding?.Subscribe();
            // 重要：默认隐藏，等待 OpenCraftingUIEvent 再显示
            SetVisible(false);
        }

        protected override void OnDisable()
        {
            // 父类自动取消订阅所有 EventBus 事件
            base.OnDisable();
            _escBinding?.Unsubscribe();
            _listManager.ReleaseAll();
            _detailPanel.ReleaseAllRequirementSlots();
            _detailPanel.ReleaseAllCraftEffectSlots();
            ClearPendingPopupRequest();
        }

        // --- 大类 / 列表模式 / 装备子分类切换 ---

        private void HandleEquipmentCategoryClicked() => SwitchCategory(CraftCategory.Equipment);
        private void HandleSynthesisCategoryClicked() => SwitchCategory(CraftCategory.Synthesis);
        // 合成大类：沿用通用的"未打造/已打造"两态切换（合成产物没有部位区分）
        private void HandleBlueprintListModeClicked() => SwitchListMode(CraftListMode.Blueprint);
        private void HandleCraftedListModeClicked() => SwitchListMode(CraftListMode.Crafted);
        // 装备大类：四个分类按钮直达"列表模式+部位"组合，一步到位，不再需要先选模式再选部位
        private void HandleWeaponBlueprintTabClicked() => SwitchSubTab(CraftListMode.Blueprint, EquipmentType.Weapon);
        private void HandleArmorBlueprintTabClicked() => SwitchSubTab(CraftListMode.Blueprint, EquipmentType.Armor);
        private void HandleWeaponCraftedTabClicked() => SwitchSubTab(CraftListMode.Crafted, EquipmentType.Weapon);
        private void HandleArmorCraftedTabClicked() => SwitchSubTab(CraftListMode.Crafted, EquipmentType.Armor);

        private void SwitchCategory(CraftCategory category)
        {
            if (_currentCategory == category) return;
            _currentCategory = category;

            // 切换大类时统一落到"未打造"这一侧的默认子分类：
            // 装备 -> 武器图纸，合成 -> 配方
            _currentListMode = CraftListMode.Blueprint;
            if (category == CraftCategory.Equipment)
                _currentSubFilter = EquipmentType.Weapon;

            ApplyCategorySpecificControlsVisibility();
            if (_isVisible) RebuildCraftList();
        }

        private void SwitchListMode(CraftListMode mode)
        {
            if (_currentListMode == mode) return;
            _currentListMode = mode;
            if (_isVisible) RebuildCraftList();
        }

        /// <summary>
        /// 装备大类专用：一次性切换"列表模式+装备部位"这一组合（对应四个直达分类按钮之一）。
        /// </summary>
        private void SwitchSubTab(CraftListMode mode, EquipmentType subFilter)
        {
            if (_currentListMode == mode && _currentSubFilter == subFilter) return;
            _currentListMode = mode;
            _currentSubFilter = subFilter;
            if (_isVisible) RebuildCraftList();
        }

        /// <summary>
        /// 按当前大类切换两组子分类按钮的显隐（六个按钮共放在同一个 SubFilter 容器下，逐个控制）：
        /// - 装备大类：显示武器图纸/防具图纸/武器/防具，隐藏配方/道具。
        /// - 合成大类：反之，显示配方（未打造）/道具（已打造），隐藏装备的四个。
        /// </summary>
        private void ApplyCategorySpecificControlsVisibility()
        {
            bool isEquipment = _currentCategory == CraftCategory.Equipment;

            if (binder.WeaponBlueprintTabButton != null) binder.WeaponBlueprintTabButton.gameObject.SetActive(isEquipment);
            if (binder.ArmorBlueprintTabButton != null) binder.ArmorBlueprintTabButton.gameObject.SetActive(isEquipment);
            if (binder.WeaponCraftedTabButton != null) binder.WeaponCraftedTabButton.gameObject.SetActive(isEquipment);
            if (binder.ArmorCraftedTabButton != null) binder.ArmorCraftedTabButton.gameObject.SetActive(isEquipment);

            if (binder.BlueprintListModeButton != null) binder.BlueprintListModeButton.gameObject.SetActive(!isEquipment);
            if (binder.CraftedListModeButton != null) binder.CraftedListModeButton.gameObject.SetActive(!isEquipment);
        }

        // --- 列表重建 ---

        private void RebuildCraftList()
        {
            CraftingSystem cs = CraftingSystem.Instance;
            ItemCategory category = _currentCategory == CraftCategory.Equipment ? ItemCategory.Equipment : ItemCategory.Consumable;

            bool hasEntries = _currentListMode == CraftListMode.Blueprint
                ? _listManager.RebuildForPrototype(cs, category, _currentSubFilter)
                : _listManager.RebuildForReplication(cs, category, _currentSubFilter);

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
            _detailPanel.RefreshCraftEffects(blueprint, isRevealed: _currentListMode == CraftListMode.Crafted);
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

            // 已打造分支：直接使用历史名称，不弹输入框
            if (_currentListMode == CraftListMode.Crafted)
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
            if (!_isVisible || _currentListMode != CraftListMode.Blueprint) return;
            if (string.IsNullOrWhiteSpace(evt.BlueprintID)) return;

            string nextKey = _listManager.RemoveByBlueprintId(evt.BlueprintID);
            if (nextKey != null) OnEntrySelected(nextKey);
            else _detailPanel.EnterEmptyState();
        }

        /// <summary>
        /// 制造历史新增事件：
        /// 未打造模式下忽略，避免把新记录回灌导致重复 Slot。
        /// </summary>
        private void HandleCraftHistoryRecorded(CraftHistoryRecordedEvent evt)
        {
            if (!_isVisible || _currentListMode == CraftListMode.Blueprint) return;
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
            _detailPanel.ReleaseAllCraftEffectSlots();
            ClearPendingPopupRequest();
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
