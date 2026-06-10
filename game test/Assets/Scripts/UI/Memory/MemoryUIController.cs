using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Memory;
using IndieGame.Gameplay.Dialogue;
using UnityEngine;

namespace IndieGame.UI.Memory
{
    /// <summary>
    /// Memory 图鉴 UI 控制器：
    /// 负责界面显隐、6 个 Tab 切换、列表构建、详情面板刷新。
    ///
    /// 架构边界：
    /// - MemoryUIBinder：只保存引用；
    /// - MemoryUIController（本类）：业务编排，EventBus 订阅，数据加载；
    /// - MemoryListManager：对象池管理与条目注册；
    /// - MemorySystem：数据权威，不关心 UI 状态；
    ///
    /// 数据加载策略：打开时全量拉取，关闭时 ReleaseAll，图鉴是积累型数据无需实时刷新。
    /// </summary>
    public class MemoryUIController : EventBusMonoBehaviour
    {
        private enum MemoryTab
        {
            Blueprint = 0,  // 图纸
            Weapon    = 1,  // 武器
            Item      = 2,  // 道具
            Material  = 3,  // 素材
            Word      = 4,  // 语料
            Task      = 5   // 任务（预留）
        }

        [Header("References")]
        [SerializeField] private MemoryUIBinder binder;

        [Header("数据源（Inspector 拖入）")]
        [Tooltip("图纸数据库，用于从 BlueprintID 反查 BlueprintSO")]
        [SerializeField] private BlueprintDatabaseSO blueprintDatabase;
        [Tooltip("全量 WordSO 数组，用于从 WordID 反查词条信息")]
        [SerializeField] private WordSO[] allWords;
        [Tooltip("全量 ItemSO 数组，用于从 ItemID 反查物品信息")]
        [SerializeField] private ItemSO[] allItems;

        [Header("Pool Settings")]
        [SerializeField] private int slotPoolWarmup = 12;

        private MemoryListManager _listManager;
        private MemoryTab _currentTab = MemoryTab.Blueprint;
        private bool _isVisible;
        private CanvasGroup _canvasGroup;

        // 运行时查找表（Awake 中构建，避免每次打开重建）
        private Dictionary<string, BlueprintSO> _blueprintById;
        private Dictionary<string, WordSO> _wordById;
        private Dictionary<string, ItemSO> _itemById;

        // 复用缓存，避免 RebuildBlueprint 时每帧分配新 List
        private readonly List<BlueprintRecord> _availableRecordsCache = new List<BlueprintRecord>();
        // 已消耗图纸 ID 快照集合（每次打开 Tab 时刷新一次）
        private readonly HashSet<string> _consumedBlueprintSnapshot = new HashSet<string>(System.StringComparer.Ordinal);

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[MemoryUIController] 缺少 MemoryUIBinder 引用。");
                return;
            }

            EnsureCanvasGroup();
            _listManager = new MemoryListManager();
            _listManager.Init(binder.SlotPrefab, binder.ListRoot, slotPoolWarmup);

            BuildLookupTables();
            HookTabButtons();
            HookCloseButton();
        }

        private void OnDestroy()
        {
            UnhookTabButtons();
            UnhookCloseButton();
        }

        protected override void OnEnable()
        {
            base.OnEnable(); // 调用 Bind()，自动订阅所有 EventBus 事件
            SetVisible(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable(); // 自动取消订阅
            _listManager?.ReleaseAll();
            ClearDetailPanel();
        }

        // ── EventBusMonoBehaviour.Bind ─────────────────────────────────────

        protected override void Bind()
        {
            Subscribe<OpenMemoryUIEvent>(HandleOpenMemoryUI);
            Subscribe<CloseMemoryUIEvent>(HandleCloseMemoryUI);
            Subscribe<MemorySlotClickedEvent>(HandleSlotClicked);
        }

        // ── 事件处理 ──────────────────────────────────────────────────────

        private void HandleOpenMemoryUI(OpenMemoryUIEvent evt)
        {
            if (!isActiveAndEnabled) return;
            _currentTab = MemoryTab.Blueprint;
            RebuildList();
            SetVisible(true);
            EventBus.Raise(new MemoryUIOpenedEvent());
        }

        private void HandleCloseMemoryUI(CloseMemoryUIEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible) return;
            _listManager.ReleaseAll();
            ClearDetailPanel();
            SetVisible(false);
            EventBus.Raise(new MemoryUIClosedEvent());
        }

        private void HandleSlotClicked(MemorySlotClickedEvent evt)
        {
            if (!_isVisible || string.IsNullOrWhiteSpace(evt.EntryKey)) return;
            if (!_listManager.TryGetEntry(evt.EntryKey, out MemoryListManager.MemoryEntry entry)) return;
            _listManager.Select(evt.EntryKey);
            ShowDetailPanel(entry);
        }

        // ── Tab 切换 ──────────────────────────────────────────────────────

        private void SwitchTab(MemoryTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            _listManager.ReleaseAll();
            ClearDetailPanel();
            if (_isVisible) RebuildList();
        }

        // ── 列表重建 ──────────────────────────────────────────────────────

        private void RebuildList()
        {
            _listManager.ReleaseAll();
            RefreshTabHighlights();

            // 任务 Tab 特殊处理：只显示占位面板，不走列表逻辑
            bool isTaskTab = _currentTab == MemoryTab.Task;
            if (binder.TaskPlaceholderPanel != null)
                binder.TaskPlaceholderPanel.SetActive(isTaskTab);
            if (binder.ListRoot != null)
                binder.ListRoot.gameObject.SetActive(!isTaskTab);
            if (binder.EmptyStateNode != null)
                binder.EmptyStateNode.SetActive(false);

            if (isTaskTab) return;

            switch (_currentTab)
            {
                case MemoryTab.Blueprint: RebuildBlueprintTab(); break;
                case MemoryTab.Weapon:    RebuildWeaponTab();    break;
                case MemoryTab.Item:      RebuildItemTab();      break;
                case MemoryTab.Material:  RebuildMaterialTab();  break;
                case MemoryTab.Word:      RebuildWordTab();      break;
            }

            // 无数据时显示空状态提示
            bool hasEntries = _listManager.EntryOrder.Count > 0;
            if (binder.EmptyStateNode != null)
                binder.EmptyStateNode.SetActive(!hasEntries);

            // 默认选中第一项以填充详情面板
            if (hasEntries)
            {
                string firstKey = _listManager.EntryOrder[0];
                if (_listManager.TryGetEntry(firstKey, out var firstEntry))
                {
                    _listManager.Select(firstKey);
                    ShowDetailPanel(firstEntry);
                }
            }
        }

        // ─── 各 Tab 构建逻辑 ──────────────────────────────────────────────

        private void RebuildBlueprintTab()
        {
            MemorySystem ms = MemorySystem.Instance;
            if (ms == null) return;

            // 刷新"已消耗"快照，避免对每条记录都调用 GetAvailableBlueprintRecords
            RefreshConsumedSnapshot();

            int idx = 0;
            foreach (string bpId in ms.ObtainedBlueprintIds)
            {
                _blueprintById.TryGetValue(bpId, out BlueprintSO bp);
                bool consumed = _consumedBlueprintSnapshot.Contains(bpId);

                _listManager.AddEntry(new MemoryListManager.MemoryEntry
                {
                    EntryKey    = "BP:" + idx++,
                    PrimaryID   = bpId,
                    DisplayName = bp != null ? bp.DefaultName : bpId,
                    Subtitle    = consumed ? "已消耗" : "持有中",
                    Description = bp?.ProductItem?.Description ?? string.Empty,
                    Icon        = bp?.GetDisplayIcon()
                });
            }
        }

        private void RebuildWeaponTab()
        {
            MemorySystem ms = MemorySystem.Instance;
            if (ms == null) return;

            var weapons = ms.CraftedWeapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                _blueprintById.TryGetValue(w.BlueprintID, out BlueprintSO bp);
                // 优先展示玩家定制名，无定制名时回退到图纸默认名
                string displayName = string.IsNullOrWhiteSpace(w.CustomName)
                    ? (bp?.DefaultName ?? w.BlueprintID)
                    : w.CustomName;

                _listManager.AddEntry(new MemoryListManager.MemoryEntry
                {
                    EntryKey    = "WP:" + i,
                    PrimaryID   = w.BlueprintID,
                    DisplayName = displayName,
                    Subtitle    = bp?.DefaultName ?? string.Empty,
                    Description = bp?.ProductItem?.Description ?? string.Empty,
                    Icon        = bp?.GetDisplayIcon()
                });
            }
        }

        private void RebuildItemTab()
        {
            RebuildItemsByCategory(false);
        }

        private void RebuildMaterialTab()
        {
            RebuildItemsByCategory(true);
        }

        /// <param name="materialOnly">true = 只显示 Material；false = 显示除 Material 以外的分类。</param>
        private void RebuildItemsByCategory(bool materialOnly)
        {
            MemorySystem ms = MemorySystem.Instance;
            if (ms == null) return;

            int idx = 0;
            foreach (string itemId in ms.SeenItemIds)
            {
                if (!_itemById.TryGetValue(itemId, out ItemSO item)) continue;
                bool isMaterial = item.Category == ItemCategory.Material;
                if (materialOnly != isMaterial) continue;

                _listManager.AddEntry(new MemoryListManager.MemoryEntry
                {
                    EntryKey    = (materialOnly ? "MT:" : "IT:") + idx++,
                    PrimaryID   = itemId,
                    DisplayName = item.GetLocalizedName(),
                    Subtitle    = CategoryToLabel(item.Category),
                    Description = item.Description ?? string.Empty,
                    Icon        = item.Icon
                });
            }
        }

        private void RebuildWordTab()
        {
            MemorySystem ms = MemorySystem.Instance;
            if (ms == null) return;

            int idx = 0;
            foreach (string wordId in ms.LearnedWordIds)
            {
                if (!_wordById.TryGetValue(wordId, out WordSO word)) continue;

                // LocalizedString.GetLocalizedString() 同步获取（已缓存时直接返回）
                string displayName  = word.DisplayName  != null ? word.DisplayName.GetLocalizedString()  : wordId;
                string description  = word.Description  != null ? word.Description.GetLocalizedString()  : string.Empty;

                _listManager.AddEntry(new MemoryListManager.MemoryEntry
                {
                    EntryKey    = "WD:" + idx++,
                    PrimaryID   = wordId,
                    DisplayName = displayName,
                    Subtitle    = "语料",
                    Description = description,
                    Icon        = null
                });
            }
        }

        // ── 详情面板 ──────────────────────────────────────────────────────

        private void ShowDetailPanel(MemoryListManager.MemoryEntry entry)
        {
            if (binder.DetailPanel != null) binder.DetailPanel.SetActive(true);
            if (binder.DetailIcon != null)
            {
                binder.DetailIcon.sprite  = entry.Icon;
                binder.DetailIcon.enabled = entry.Icon != null;
            }
            if (binder.DetailNameText     != null) binder.DetailNameText.text     = entry.DisplayName;
            if (binder.DetailSubtitleText != null) binder.DetailSubtitleText.text = entry.Subtitle;
            if (binder.DetailDescText     != null) binder.DetailDescText.text     = entry.Description;
        }

        private void ClearDetailPanel()
        {
            if (binder == null || binder.DetailPanel == null) return;
            binder.DetailPanel.SetActive(false);
        }

        // ── 辅助方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 刷新已消耗图纸快照：
        /// 调用 CraftingSystem.GetAvailableBlueprintRecords 得到未消耗记录，
        /// 反推出"不在可用列表中 = 已消耗"的 ID 集合。
        /// </summary>
        private void RefreshConsumedSnapshot()
        {
            _consumedBlueprintSnapshot.Clear();
            MemorySystem ms = MemorySystem.Instance;
            if (ms == null) return;

            CraftingSystem cs = CraftingSystem.Instance;
            if (cs == null)
            {
                // 无法查询时保守处理：所有图纸视为持有中
                return;
            }

            _availableRecordsCache.Clear();
            cs.GetAvailableBlueprintRecords(_availableRecordsCache);

            // 将可用 ID 存入临时集合，用于 O(1) 查找
            var availableSet = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var rec in _availableRecordsCache)
                if (rec != null && !string.IsNullOrEmpty(rec.ID))
                    availableSet.Add(rec.ID);

            // 不在可用集合中的，标记为已消耗
            foreach (string bpId in ms.ObtainedBlueprintIds)
                if (!availableSet.Contains(bpId))
                    _consumedBlueprintSnapshot.Add(bpId);
        }

        private void RefreshTabHighlights()
        {
            if (binder.TabHighlights == null) return;
            int active = (int)_currentTab;
            for (int i = 0; i < binder.TabHighlights.Length; i++)
                if (binder.TabHighlights[i] != null)
                    binder.TabHighlights[i].SetActive(i == active);
        }

        private void BuildLookupTables()
        {
            _blueprintById = new Dictionary<string, BlueprintSO>(System.StringComparer.Ordinal);
            if (blueprintDatabase?.Blueprints != null)
                foreach (var bp in blueprintDatabase.Blueprints)
                    if (bp != null && !string.IsNullOrWhiteSpace(bp.ID))
                        _blueprintById[bp.ID] = bp;

            _wordById = new Dictionary<string, WordSO>(System.StringComparer.Ordinal);
            if (allWords != null)
                foreach (var w in allWords)
                    if (w != null && !string.IsNullOrWhiteSpace(w.ID))
                        _wordById[w.ID] = w;

            _itemById = new Dictionary<string, ItemSO>(System.StringComparer.Ordinal);
            if (allItems != null)
                foreach (var item in allItems)
                    if (item != null && !string.IsNullOrWhiteSpace(item.ID))
                        _itemById[item.ID] = item;
        }

        private void EnsureCanvasGroup()
        {
            _canvasGroup = binder != null
                ? binder.CanvasGroup ?? GetComponent<CanvasGroup>()
                : GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup == null) return;
            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable   = visible;
        }

        private void HookTabButtons()
        {
            if (binder?.TabButtons == null) return;
            for (int i = 0; i < binder.TabButtons.Length; i++)
            {
                int tabIndex = i;
                binder.TabButtons[i]?.onClick.AddListener(() => SwitchTab((MemoryTab)tabIndex));
            }
        }

        private void UnhookTabButtons()
        {
            if (binder?.TabButtons == null) return;
            foreach (var btn in binder.TabButtons) btn?.onClick.RemoveAllListeners();
        }

        private void HookCloseButton()
        {
            binder?.CloseButton?.onClick.AddListener(() => EventBus.Raise(new CloseMemoryUIEvent()));
        }

        private void UnhookCloseButton()
        {
            binder?.CloseButton?.onClick.RemoveAllListeners();
        }

        private static string CategoryToLabel(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Equipment:  return "装备";
                case ItemCategory.Consumable: return "消耗品";
                case ItemCategory.Material:   return "素材";
                case ItemCategory.Quest:      return "任务物品";
                default:                      return "未知";
            }
        }
    }
}
