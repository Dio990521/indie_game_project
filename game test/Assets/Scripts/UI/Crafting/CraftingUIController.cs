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
    /// 架构职责边界：
    /// - CraftUIBinder：只提供引用（不写逻辑）
    /// - CraftingUIController：负责列表生成、选中切换、按钮状态刷新、输入关闭
    /// - CraftingSystem：负责制造规则与数据
    ///
    /// 刷新规则（本类严格执行）：
    /// 1) 左侧列表：显示图纸图标 + 图纸固定名称（名称来自 BlueprintSO.DefaultName）
    /// 2) 右侧详情：只显示“成品图标 + 材料清单 + 制造按钮”
    ///    不显示成品名称文本（按需求明确移除）
    /// </summary>
    public class CraftingUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CraftUIBinder binder;
        [SerializeField] private GameInputReader inputReader;

        [Header("Pool Settings")]
        [Tooltip("左侧图纸列表对象池预热数量")]
        [SerializeField] private int slotPoolWarmup = 8;

        [Tooltip("右侧材料列表对象池预热数量")]
        [SerializeField] private int requirementPoolWarmup = 10;

        // 左侧图纸 Slot 对象池
        private GameObjectPool _slotPool;
        // 右侧材料条目对象池
        private GameObjectPool _requirementPool;

        // 当前激活的左侧 Slot 实例（用于回收）
        private readonly List<BlueprintSlotUI> _activeBlueprintSlots = new List<BlueprintSlotUI>();
        // 当前激活的右侧材料行实例（用于回收）
        private readonly List<RequirementSlotUI> _activeRequirementSlots = new List<RequirementSlotUI>();

        // BlueprintID -> SlotUI 的快速映射，支持 O(1) 删除
        private readonly Dictionary<string, BlueprintSlotUI> _slotByBlueprintId = new Dictionary<string, BlueprintSlotUI>(StringComparer.Ordinal);
        // 保留左侧顺序，便于“移除后自动选中第一个”
        private readonly List<string> _blueprintOrder = new List<string>();

        // 临时缓存：可用图纸记录列表（避免重复 new）
        private readonly List<BlueprintRecord> _availableRecordsCache = new List<BlueprintRecord>();
        // 临时缓存：背包数量统计（ItemSO -> 拥有数量）
        private readonly Dictionary<ItemSO, int> _inventoryCountCache = new Dictionary<ItemSO, int>();

        // 当前选中的图纸 ID（右侧详情绑定该 ID）
        private string _selectedBlueprintId;
        // 当前界面是否处于显示状态（由本 Controller 通过 EventBus 控制）
        private bool _isVisible;
        // 用于软显示/软隐藏的 CanvasGroup（避免用 SetActive 导致监听失效）
        private CanvasGroup _canvasGroup;

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
        }

        private void OnDestroy()
        {
            if (binder != null && binder.CraftButton != null)
            {
                binder.CraftButton.onClick.RemoveListener(HandleCraftButtonClicked);
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
            SubscribeInput();
            // 首次启用时默认隐藏，等待 OpenCraftingUIEvent 再显示
            SetVisible(false);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            UnsubscribeInput();

            // 关闭界面时回收所有动态对象，避免重复启用时出现旧残留
            ReleaseAllBlueprintSlots();
            ReleaseAllRequirementSlots();
        }

        /// <summary>
        /// 从 CraftingSystem 读取“未消耗图纸”并重建左侧列表。
        /// </summary>
        private void RebuildBlueprintList()
        {
            ReleaseAllBlueprintSlots();

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null)
            {
                EnterEmptyState();
                return;
            }

            craftingSystem.GetAvailableBlueprintRecords(_availableRecordsCache);
            for (int i = 0; i < _availableRecordsCache.Count; i++)
            {
                BlueprintRecord record = _availableRecordsCache[i];
                if (record == null || string.IsNullOrWhiteSpace(record.ID)) continue;

                BlueprintSO data = craftingSystem.GetBlueprint(record.ID);
                if (data == null) continue;

                BlueprintSlotUI slotUI = SpawnBlueprintSlot(record, data);
                if (slotUI == null) continue;

                string blueprintId = record.ID;

                _activeBlueprintSlots.Add(slotUI);
                _slotByBlueprintId[blueprintId] = slotUI;
                _blueprintOrder.Add(blueprintId);
            }

            // 自动选中逻辑：
            // - 有图纸 -> 自动点亮第 0 个
            // - 无图纸 -> 切换空状态
            if (_blueprintOrder.Count > 0)
            {
                OnBlueprintSelected(_blueprintOrder[0]);
            }
            else
            {
                EnterEmptyState();
            }
        }

        /// <summary>
        /// 选中图纸后的统一刷新入口。
        /// </summary>
        private void OnBlueprintSelected(string blueprintId)
        {
            if (string.IsNullOrWhiteSpace(blueprintId))
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

            BlueprintSO data = craftingSystem.GetBlueprint(blueprintId);
            if (data == null)
            {
                EnterEmptyState();
                return;
            }

            _selectedBlueprintId = blueprintId;

            // 选中后强制隐藏空状态
            if (binder.EmptyStateNode != null)
            {
                binder.EmptyStateNode.SetActive(false);
            }

            // 右侧详情显示规则（严格执行需求）：
            // 1) 只刷新成品图标，不显示成品名称
            // 2) 刷新材料清单
            // 3) 刷新按钮可点击状态
            SetDetailPanelVisible(true);

            if (binder.ProductIcon != null)
            {
                binder.ProductIcon.sprite = data.GetDisplayIcon();
                binder.ProductIcon.enabled = binder.ProductIcon.sprite != null;
            }

            RefreshRequirementList();
            RefreshButtonState();
        }

        /// <summary>
        /// 刷新“右侧材料清单”。
        /// </summary>
        private void RefreshRequirementList()
        {
            ReleaseAllRequirementSlots();

            if (string.IsNullOrWhiteSpace(_selectedBlueprintId)) return;

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null) return;

            BlueprintSO data = craftingSystem.GetBlueprint(_selectedBlueprintId);
            if (data == null) return;
            if (binder.RequirementsRoot == null) return;

            RebuildInventoryCountCache();

            IReadOnlyList<BlueprintRequirement> requirements = data.Requirements;
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
        /// 刷新制造按钮状态：
        /// - 材料不足 -> 不可点击
        /// - 材料充足 -> 可点击
        ///
        /// 调用时机：
        /// - 选中图纸后
        /// - 收到 OnInventoryChanged 后
        /// </summary>
        private void RefreshButtonState()
        {
            if (binder.CraftButton == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedBlueprintId))
            {
                binder.CraftButton.interactable = false;
                return;
            }

            CraftingSystem craftingSystem = CraftingSystem.Instance;
            bool canCraft = craftingSystem != null && craftingSystem.CanCraft(_selectedBlueprintId);
            binder.CraftButton.interactable = canCraft;
        }

        /// <summary>
        /// 制造按钮点击：
        /// 只发起制造，不直接改 UI 列表；
        /// UI 的移除由 OnBlueprintConsumed 事件统一驱动，保证解耦与一致性。
        /// </summary>
        private void HandleCraftButtonClicked()
        {
            if (!_isVisible) return;
            if (string.IsNullOrWhiteSpace(_selectedBlueprintId)) return;
            CraftingSystem craftingSystem = CraftingSystem.Instance;
            if (craftingSystem == null) return;
            craftingSystem.ExecuteCraft(_selectedBlueprintId);
        }

        /// <summary>
        /// 背包变化事件处理：
        /// 当材料数量变化时，刷新右侧材料拥有数与按钮状态。
        /// </summary>
        private void HandleInventoryChanged(OnInventoryChanged evt)
        {
            if (!_isVisible) return;
            if (string.IsNullOrWhiteSpace(_selectedBlueprintId)) return;
            RefreshRequirementList();
            RefreshButtonState();
        }

        /// <summary>
        /// 图纸消耗事件处理：
        /// - 移除左侧对应 Slot
        /// - 若该图纸正被选中：自动选第一个；如果列表空则进入空状态
        /// </summary>
        private void HandleBlueprintConsumed(OnBlueprintConsumed evt)
        {
            if (!_isVisible) return;
            if (string.IsNullOrWhiteSpace(evt.BlueprintID)) return;
            if (!_slotByBlueprintId.TryGetValue(evt.BlueprintID, out BlueprintSlotUI slotUI)) return;

            // 从左侧列表移除对应图纸
            RemoveBlueprintSlot(evt.BlueprintID, slotUI);

            // 如果消耗的是当前选中图纸，按需求执行“自动选中第一个/空状态切换”
            if (string.Equals(_selectedBlueprintId, evt.BlueprintID, StringComparison.Ordinal))
            {
                if (_blueprintOrder.Count > 0)
                {
                    OnBlueprintSelected(_blueprintOrder[0]);
                }
                else
                {
                    EnterEmptyState();
                }
                return;
            }

            // 若当前选中未变化，也刷新一次按钮，确保状态与库存一致
            RefreshButtonState();
        }

        /// <summary>
        /// 图纸槽位点击事件处理：
        /// 由 BlueprintSlotUI 通过 EventBus 广播，控制器接收后执行选中刷新。
        /// </summary>
        private void HandleBlueprintSlotClicked(CraftBlueprintSlotClickedEvent evt)
        {
            if (!isActiveAndEnabled) return;
            if (!_isVisible) return;
            if (string.IsNullOrWhiteSpace(evt.BlueprintID)) return;
            if (!_slotByBlueprintId.ContainsKey(evt.BlueprintID)) return;
            OnBlueprintSelected(evt.BlueprintID);
        }

        /// <summary>
        /// 打开打造界面事件：
        /// 由外部入口（如 Camp 按钮）通过 EventBus 广播。
        /// </summary>
        private void HandleOpenCraftingUI(OpenCraftingUIEvent evt)
        {
            if (!isActiveAndEnabled) return;
            RebuildBlueprintList();
            SetVisible(true);
        }

        /// <summary>
        /// 关闭打造界面事件：
        /// 统一收拢隐藏与清理逻辑。
        /// </summary>
        private void HandleCloseCraftingUI(CloseCraftingUIEvent evt)
        {
            if (!isActiveAndEnabled) return;
            if (!_isVisible) return;
            SetVisible(false);
            ReleaseAllBlueprintSlots();
            ReleaseAllRequirementSlots();
            _selectedBlueprintId = null;
        }

        /// <summary>
        /// 进入空状态：
        /// - 显示 emptyStateNode
        /// - 隐藏右侧图标、材料区、按钮
        /// </summary>
        private void EnterEmptyState()
        {
            _selectedBlueprintId = null;

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
        ///
        /// 说明：
        /// 右侧不包含成品名称文本，仅包含成品图标 + 材料列表 + 按钮。
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
        /// ESC/UI Cancel 关闭界面。
        /// </summary>
        private void HandleUICancel()
        {
            if (!isActiveAndEnabled) return;
            if (!_isVisible) return;
            // 关闭动作同样走 EventBus，保证显隐入口一致。
            EventBus.Raise(new CloseCraftingUIEvent());
        }

        private void SubscribeEvents()
        {
            EventBus.Subscribe<OnInventoryChanged>(HandleInventoryChanged);
            EventBus.Subscribe<OnBlueprintConsumed>(HandleBlueprintConsumed);
            EventBus.Subscribe<CraftBlueprintSlotClickedEvent>(HandleBlueprintSlotClicked);
            EventBus.Subscribe<OpenCraftingUIEvent>(HandleOpenCraftingUI);
            EventBus.Subscribe<CloseCraftingUIEvent>(HandleCloseCraftingUI);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<OnInventoryChanged>(HandleInventoryChanged);
            EventBus.Unsubscribe<OnBlueprintConsumed>(HandleBlueprintConsumed);
            EventBus.Unsubscribe<CraftBlueprintSlotClickedEvent>(HandleBlueprintSlotClicked);
            EventBus.Unsubscribe<OpenCraftingUIEvent>(HandleOpenCraftingUI);
            EventBus.Unsubscribe<CloseCraftingUIEvent>(HandleCloseCraftingUI);
        }

        /// <summary>
        /// 输入订阅安全：
        /// 明确在 OnEnable/OnDisable 中成对订阅/注销 GameInputReader.UICancelEvent。
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
        /// 创建左侧图纸项（对象池）。
        /// </summary>
        private BlueprintSlotUI SpawnBlueprintSlot(BlueprintRecord record, BlueprintSO data)
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

            slotUI.Setup(record, data);
            return slotUI;
        }

        /// <summary>
        /// 创建右侧材料条目（对象池）。
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
        /// 删除单个左侧图纸项并回收。
        /// </summary>
        private void RemoveBlueprintSlot(string blueprintId, BlueprintSlotUI slotUI)
        {
            if (slotUI != null && _slotPool != null)
            {
                _slotPool.Release(slotUI.gameObject);
            }

            _activeBlueprintSlots.Remove(slotUI);
            _slotByBlueprintId.Remove(blueprintId);
            _blueprintOrder.Remove(blueprintId);
        }

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
            _slotByBlueprintId.Clear();
            _blueprintOrder.Clear();
        }

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
        /// 重建背包数量缓存（用于右侧材料“拥有数”展示）。
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
        /// Craft UI 通过软显隐控制，不依赖 GameObject.SetActive。
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
        /// 软显隐实现：
        /// - visible = true：可见 + 可交互
        /// - visible = false：透明 + 不可交互
        /// </summary>
        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup == null)
            {
                EnsureCanvasGroup();
            }

            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
