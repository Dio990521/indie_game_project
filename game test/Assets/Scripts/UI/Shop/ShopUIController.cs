using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Economy;
using IndieGame.Gameplay.Shop;

namespace IndieGame.UI.Shop
{
    /// <summary>
    /// 商店 UI 控制器（Controller）：
    /// 负责商店界面的全部交互流程与 UI 刷新。
    ///
    /// 核心职责：
    /// 1) 监听 Open/CloseShopUI 事件，控制界面显示与隐藏；
    /// 2) 用对象池构建左侧商品列表；
    /// 3) 处理商品选中、右侧详情刷新、购买按钮状态；
    /// 4) 处理数量弹窗（左右箭头增减数量、确认/取消）；
    /// 5) 监听 GoldChangedEvent 实时刷新金币与可购买上限；
    /// 6) 监听 GameInputReader.UICancelEvent，实现 ESC 关闭逻辑。
    ///
    /// ESC 规则（与 Craft 一致）：
    /// - 若数量弹窗打开：优先关闭数量弹窗；
    /// - 否则：关闭整个商店界面。
    /// </summary>
    public class ShopUIController : MonoBehaviour
    {
        /// <summary>
        /// 列表展示条目（UI 适配层）：
        /// 把 ShopSystem 的数据映射到统一的 UI 展示结构。
        /// </summary>
        private struct ShopListEntry
        {
            public string EntryKey;
            public string ShopID;
            public string ShopEntryID;
            public string DisplayName;
            public string Description;
            public Sprite Icon;
            public int UnitPrice;
        }

        [Header("References")]
        [SerializeField] private ShopUIBinder binder;
        [SerializeField] private GameInputReader inputReader;

        [Header("Pool Settings")]
        [Tooltip("商品列表对象池预热数量。")]
        [SerializeField] private int slotPoolWarmup = 12;

        // UI 对象池：用于左侧商品列表，避免频繁创建销毁。
        private GameObjectPool _slotPool;
        // CanvasGroup：软显隐，不关闭 GameObject，确保控制器持续监听 EventBus。
        private CanvasGroup _canvasGroup;

        // 当前激活的 slot 列表（用于回收）
        private readonly List<ShopItemSlotUI> _activeSlots = new List<ShopItemSlotUI>();
        // EntryKey -> 列表项数据
        private readonly Dictionary<string, ShopListEntry> _entryByKey = new Dictionary<string, ShopListEntry>(StringComparer.Ordinal);
        // EntryKey 顺序表（用于默认选中第一个条目）
        private readonly List<string> _entryOrder = new List<string>();
        // 缓存：读取 ShopSystem 条目时复用
        private readonly List<ShopItemEntry> _shopEntryBuffer = new List<ShopItemEntry>();

        // 当前商店 UI 状态
        private bool _isVisible;
        private string _currentShopId;
        private string _selectedEntryKey;

        // 进入商店前状态快照（用于关闭时恢复）
        private bool _hasStateSnapshot;
        private GameState _stateBeforeShop = GameState.FreeRoam;

        // 数量弹窗上下文
        private bool _isQuantityPopupOpen;
        private string _popupShopEntryId;
        private int _popupQuantity;
        private int _popupMaxQuantity;
        private int _popupUnitPrice;

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[ShopUIController] Missing ShopUIBinder reference.");
                return;
            }

            EnsureCanvasGroup();
            EnsurePool();
            HookButtons();
            SetVisible(false);
            SetQuantityPopupVisible(false);
        }

        private void OnDestroy()
        {
            UnhookButtons();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            SubscribeInput();
            SetVisible(false);
            SetQuantityPopupVisible(false);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            UnsubscribeInput();

            ReleaseAllSlots();
            ClearSelectionAndDetail();
            SetQuantityPopupVisible(false);

            // 保险恢复：
            // 即使对象因场景切换被禁用，也尽量把游戏状态恢复，避免玩家被卡在 Paused。
            RestoreStateAfterShopIfNeeded();
        }

        /// <summary>
        /// 打开商店事件处理。
        /// </summary>
        private void HandleOpenShopUIRequest(OpenShopUIRequestEvent evt)
        {
            if (!isActiveAndEnabled) return;
            if (string.IsNullOrWhiteSpace(evt.ShopID)) return;

            _currentShopId = evt.ShopID.Trim();
            CacheStateBeforeShopAndEnterPaused();
            RebuildList(preferredShopEntryId: null);
            RefreshGoldDisplay();
            SetVisible(true);
        }

        /// <summary>
        /// 关闭商店事件处理。
        /// </summary>
        private void HandleCloseShopUIRequest(CloseShopUIRequestEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible) return;

            SetQuantityPopupVisible(false);
            ReleaseAllSlots();
            ClearSelectionAndDetail();
            SetVisible(false);
            RestoreStateAfterShopIfNeeded();
        }

        /// <summary>
        /// 商品 Slot 点击事件处理。
        /// </summary>
        private void HandleShopItemSlotClicked(ShopItemSlotClickedEvent evt)
        {
            if (!_isVisible) return;
            if (!string.Equals(evt.ShopID, _currentShopId, StringComparison.Ordinal)) return;
            if (string.IsNullOrWhiteSpace(evt.EntryKey)) return;
            if (!_entryByKey.ContainsKey(evt.EntryKey)) return;

            OnEntrySelected(evt.EntryKey);
        }

        /// <summary>
        /// 金币变化时刷新：
        /// 1) 左下角金币显示；
        /// 2) 购买按钮可用状态；
        /// 3) 若数量弹窗打开，实时更新可选上限与总价。
        /// </summary>
        private void HandleGoldChangedEvent(GoldChangedEvent evt)
        {
            if (!_isVisible) return;

            RefreshGoldDisplay();
            RefreshBuyButtonState();
            RefreshQuantityPopupState();
        }

        /// <summary>
        /// 购买成功事件：
        /// 若当前商店正在打开，则重建列表并尽量保持原商品选中，实时更新库存/限购状态。
        /// </summary>
        private void HandleShopPurchaseCompletedEvent(ShopPurchaseCompletedEvent evt)
        {
            if (!_isVisible) return;
            if (!string.Equals(evt.ShopID, _currentShopId, StringComparison.Ordinal)) return;

            RebuildList(preferredShopEntryId: evt.ShopEntryID);
            RefreshGoldDisplay();
        }

        /// <summary>
        /// 购买按钮点击：
        /// 不立即交易，先弹数量选择框。
        /// </summary>
        private void HandleBuyButtonClicked()
        {
            if (!_isVisible) return;
            if (!TryGetSelectedEntry(out ShopListEntry selected)) return;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null) return;

            int maxPurchasable = shopSystem.GetMaxPurchasableQuantity(selected.ShopID, selected.ShopEntryID);
            if (maxPurchasable <= 0)
            {
                RefreshBuyButtonState();
                return;
            }

            _popupShopEntryId = selected.ShopEntryID;
            _popupUnitPrice = selected.UnitPrice;
            _popupMaxQuantity = maxPurchasable;
            _popupQuantity = Mathf.Clamp(1, 1, _popupMaxQuantity);

            SetQuantityPopupVisible(true);
            RefreshQuantityPopupVisual();
        }

        /// <summary>
        /// 数量弹窗“减少”按钮。
        /// </summary>
        private void HandleDecreaseClicked()
        {
            if (!_isQuantityPopupOpen) return;
            _popupQuantity = Mathf.Max(1, _popupQuantity - 1);
            RefreshQuantityPopupVisual();
        }

        /// <summary>
        /// 数量弹窗“增加”按钮。
        /// </summary>
        private void HandleIncreaseClicked()
        {
            if (!_isQuantityPopupOpen) return;
            _popupQuantity = Mathf.Min(_popupMaxQuantity, _popupQuantity + 1);
            RefreshQuantityPopupVisual();
        }

        /// <summary>
        /// 数量弹窗“取消”按钮。
        /// </summary>
        private void HandleCancelPurchaseClicked()
        {
            SetQuantityPopupVisible(false);
        }

        /// <summary>
        /// 数量弹窗“确认”按钮：
        /// 真正调用 ShopSystem.TryPurchase 执行交易。
        /// </summary>
        private void HandleConfirmPurchaseClicked()
        {
            if (!_isQuantityPopupOpen) return;
            if (string.IsNullOrWhiteSpace(_currentShopId) || string.IsNullOrWhiteSpace(_popupShopEntryId)) return;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null) return;

            ShopPurchaseResult result = shopSystem.TryPurchase(_currentShopId, _popupShopEntryId, _popupQuantity);
            if (!result.Success)
            {
                Debug.LogWarning($"[ShopUIController] Purchase failed: {result.FailReason} - {result.Message}");
                RefreshBuyButtonState();
                RefreshQuantityPopupState();
                return;
            }

            // 成功后关闭弹窗，列表刷新由 ShopPurchaseCompletedEvent 驱动。
            SetQuantityPopupVisible(false);
        }

        /// <summary>
        /// ESC / Cancel 输入：
        /// - 先关数量弹窗；
        /// - 再关商店界面。
        /// </summary>
        private void HandleUICancel()
        {
            if (!_isVisible) return;

            if (_isQuantityPopupOpen)
            {
                SetQuantityPopupVisible(false);
                return;
            }

            EventBus.Raise(new CloseShopUIRequestEvent());
        }

        /// <summary>
        /// 重建左侧商品列表，并自动选中条目。
        /// </summary>
        private void RebuildList(string preferredShopEntryId)
        {
            ReleaseAllSlots();
            _selectedEntryKey = string.Empty;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null)
            {
                EnterEmptyState();
                return;
            }

            shopSystem.GetEntries(_currentShopId, _shopEntryBuffer);
            for (int i = 0; i < _shopEntryBuffer.Count; i++)
            {
                ShopItemEntry entry = _shopEntryBuffer[i];
                if (entry == null || entry.Item == null) continue;

                string entryKey = $"SHOP:{i}:{entry.EntryID}";
                string displayName = ResolveItemDisplayName(entry.Item);
                string description = string.IsNullOrWhiteSpace(entry.Item.Description)
                    ? "No description available."
                    : entry.Item.Description;

                ShopListEntry listEntry = new ShopListEntry
                {
                    EntryKey = entryKey,
                    ShopID = _currentShopId,
                    ShopEntryID = entry.EntryID,
                    DisplayName = displayName,
                    Description = description,
                    Icon = entry.Item.Icon,
                    UnitPrice = entry.UnitPrice
                };

                AddListEntry(listEntry);
            }

            if (_entryOrder.Count == 0)
            {
                EnterEmptyState();
                return;
            }

            string targetEntryKey = FindEntryKeyByShopEntryId(preferredShopEntryId);
            if (string.IsNullOrWhiteSpace(targetEntryKey))
            {
                targetEntryKey = _entryOrder[0];
            }

            OnEntrySelected(targetEntryKey);
        }

        /// <summary>
        /// 将单条商品加入列表（UI + 索引）。
        /// </summary>
        private void AddListEntry(ShopListEntry entry)
        {
            ShopItemSlotUI slotUI = SpawnSlot(entry);
            if (slotUI == null) return;

            _activeSlots.Add(slotUI);
            _entryByKey[entry.EntryKey] = entry;
            _entryOrder.Add(entry.EntryKey);
        }

        /// <summary>
        /// 列表选中后刷新右侧详情与按钮状态。
        /// </summary>
        private void OnEntrySelected(string entryKey)
        {
            if (!_entryByKey.TryGetValue(entryKey, out ShopListEntry entry))
            {
                EnterEmptyState();
                return;
            }

            _selectedEntryKey = entryKey;

            if (binder.EmptyStateNode != null)
            {
                binder.EmptyStateNode.SetActive(false);
            }

            RefreshDetailText(entry);
            RefreshBuyButtonState();
            RefreshQuantityPopupState();
        }

        /// <summary>
        /// 刷新右侧描述：
        /// 在基础描述下附加价格/库存/限购信息，便于玩家快速决策。
        /// </summary>
        private void RefreshDetailText(ShopListEntry entry)
        {
            TMP_Text text = binder.DescriptionText;
            if (text == null) return;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null)
            {
                text.text = entry.Description ?? string.Empty;
                return;
            }

            int stock = shopSystem.GetRemainingStock(entry.ShopID, entry.ShopEntryID);
            int quota = shopSystem.GetRemainingPurchaseQuota(entry.ShopID, entry.ShopEntryID);

            string stockText = stock < 0 ? "Stock: Unlimited" : $"Stock: {stock}";
            string quotaText = quota < 0 ? "Purchase Quota: Unlimited" : $"Remaining Purchase Quota: {quota}";

            text.text = $"{entry.Description}\n\nUnit Price: {entry.UnitPrice} G\n{stockText}\n{quotaText}";
        }

        /// <summary>
        /// 刷新购买按钮状态：
        /// 只要当前选中条目“可买数量 > 0”，按钮即可交互。
        /// </summary>
        private void RefreshBuyButtonState()
        {
            if (binder.BuyButton == null)
            {
                return;
            }

            if (!TryGetSelectedEntry(out ShopListEntry selected))
            {
                binder.BuyButton.interactable = false;
                return;
            }

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null)
            {
                binder.BuyButton.interactable = false;
                return;
            }

            int max = shopSystem.GetMaxPurchasableQuantity(selected.ShopID, selected.ShopEntryID);
            binder.BuyButton.interactable = max > 0;
        }

        /// <summary>
        /// 刷新左下角金币显示。
        /// </summary>
        private void RefreshGoldDisplay()
        {
            TMP_Text goldText = binder.GoldValueText;
            if (goldText == null) return;

            GoldSystem goldSystem = GoldSystem.Instance;
            int currentGold = goldSystem != null ? goldSystem.CurrentGold : 0;
            goldText.text = $"Gold: {currentGold}";
        }

        /// <summary>
        /// 当外部状态变化（金币/库存/限购）时，刷新数量弹窗上下文并重绘。
        /// </summary>
        private void RefreshQuantityPopupState()
        {
            if (!_isQuantityPopupOpen) return;
            if (!TryGetSelectedEntry(out ShopListEntry selected))
            {
                SetQuantityPopupVisible(false);
                return;
            }

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null)
            {
                SetQuantityPopupVisible(false);
                return;
            }

            _popupMaxQuantity = shopSystem.GetMaxPurchasableQuantity(selected.ShopID, selected.ShopEntryID);
            _popupUnitPrice = selected.UnitPrice;
            _popupShopEntryId = selected.ShopEntryID;

            if (_popupMaxQuantity <= 0)
            {
                SetQuantityPopupVisible(false);
                return;
            }

            _popupQuantity = Mathf.Clamp(_popupQuantity, 1, _popupMaxQuantity);
            RefreshQuantityPopupVisual();
        }

        /// <summary>
        /// 刷新数量弹窗文本与按钮可交互。
        /// </summary>
        private void RefreshQuantityPopupVisual()
        {
            if (!_isQuantityPopupOpen) return;

            if (binder.QuantityValueText != null)
            {
                binder.QuantityValueText.text = _popupQuantity.ToString();
            }

            if (binder.TotalPriceValueText != null)
            {
                long total = (long)_popupUnitPrice * _popupQuantity;
                binder.TotalPriceValueText.text = $"{total} G";
            }

            if (binder.DecreaseButton != null)
            {
                binder.DecreaseButton.interactable = _popupQuantity > 1;
            }

            if (binder.IncreaseButton != null)
            {
                binder.IncreaseButton.interactable = _popupQuantity < _popupMaxQuantity;
            }

            if (binder.ConfirmButton != null)
            {
                binder.ConfirmButton.interactable = _popupMaxQuantity > 0;
            }
        }

        /// <summary>
        /// 进入空状态（无商品或无效商店）。
        /// </summary>
        private void EnterEmptyState()
        {
            _selectedEntryKey = string.Empty;

            if (binder.EmptyStateNode != null)
            {
                binder.EmptyStateNode.SetActive(true);
            }

            if (binder.DescriptionText != null)
            {
                binder.DescriptionText.text = "No purchasable items available.";
            }

            if (binder.BuyButton != null)
            {
                binder.BuyButton.interactable = false;
            }

            SetQuantityPopupVisible(false);
        }

        /// <summary>
        /// 清空选中与详情显示。
        /// </summary>
        private void ClearSelectionAndDetail()
        {
            _selectedEntryKey = string.Empty;
            _currentShopId = string.Empty;

            if (binder.DescriptionText != null)
            {
                binder.DescriptionText.text = string.Empty;
            }

            if (binder.BuyButton != null)
            {
                binder.BuyButton.interactable = false;
            }

            if (binder.GoldValueText != null)
            {
                binder.GoldValueText.text = "Gold: 0";
            }
        }

        /// <summary>
        /// 通过对象池创建列表项。
        /// </summary>
        private ShopItemSlotUI SpawnSlot(ShopListEntry entry)
        {
            if (_slotPool == null || binder.ListRoot == null) return null;

            GameObject go = _slotPool.Get();
            go.transform.SetParent(binder.ListRoot, false);

            ShopItemSlotUI slotUI = go.GetComponent<ShopItemSlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("[ShopUIController] Slot prefab missing ShopItemSlotUI component.");
                _slotPool.Release(go);
                return null;
            }

            slotUI.Setup(entry.EntryKey, entry.ShopID, entry.ShopEntryID, entry.Icon, entry.DisplayName, entry.UnitPrice);
            return slotUI;
        }

        /// <summary>
        /// 回收所有列表项并清空索引。
        /// </summary>
        private void ReleaseAllSlots()
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                ShopItemSlotUI slot = _activeSlots[i];
                if (slot == null) continue;
                if (_slotPool != null)
                {
                    _slotPool.Release(slot.gameObject);
                }
            }

            _activeSlots.Clear();
            _entryByKey.Clear();
            _entryOrder.Clear();
        }

        /// <summary>
        /// 查找指定 ShopEntryID 对应的 EntryKey。
        /// </summary>
        private string FindEntryKeyByShopEntryId(string shopEntryId)
        {
            if (string.IsNullOrWhiteSpace(shopEntryId)) return string.Empty;
            string target = shopEntryId.Trim();

            for (int i = 0; i < _entryOrder.Count; i++)
            {
                string key = _entryOrder[i];
                if (!_entryByKey.TryGetValue(key, out ShopListEntry entry)) continue;
                if (!string.Equals(entry.ShopEntryID, target, StringComparison.Ordinal)) continue;
                return key;
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取当前选中条目。
        /// </summary>
        private bool TryGetSelectedEntry(out ShopListEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(_selectedEntryKey)) return false;
            return _entryByKey.TryGetValue(_selectedEntryKey, out entry);
        }

        /// <summary>
        /// 从 ItemSO 解析显示名称（本地化优先）。
        /// </summary>
        private static string ResolveItemDisplayName(IndieGame.Gameplay.Inventory.ItemSO item)
        {
            if (item == null) return "Unknown Item";

            if (item.ItemName != null)
            {
                string localized = item.ItemName.GetLocalizedString();
                if (!string.IsNullOrWhiteSpace(localized)) return localized;
            }

            if (!string.IsNullOrWhiteSpace(item.ID)) return item.ID.Trim();
            return "Unknown Item";
        }

        /// <summary>
        /// 记录打开商店前的状态，并在 FreeRoam 下切到 Paused，阻止玩家移动。
        /// </summary>
        private void CacheStateBeforeShopAndEnterPaused()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            _stateBeforeShop = gameManager.CurrentState;
            _hasStateSnapshot = true;

            if (gameManager.CurrentState == GameState.FreeRoam)
            {
                gameManager.ChangeState(GameState.Paused);
            }
        }

        /// <summary>
        /// 商店关闭后恢复状态：
        /// 仅当当前仍是 Paused 才执行恢复，避免覆盖其它系统主动切换的新状态。
        /// </summary>
        private void RestoreStateAfterShopIfNeeded()
        {
            if (!_hasStateSnapshot) return;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                _hasStateSnapshot = false;
                return;
            }

            if (gameManager.CurrentState == GameState.Paused)
            {
                GameState restoreState = _stateBeforeShop == GameState.Paused
                    ? GameState.FreeRoam
                    : _stateBeforeShop;
                gameManager.ChangeState(restoreState);
            }

            _hasStateSnapshot = false;
        }

        /// <summary>
        /// 显示/隐藏数量弹窗。
        /// </summary>
        private void SetQuantityPopupVisible(bool visible)
        {
            _isQuantityPopupOpen = visible;
            if (!visible)
            {
                _popupShopEntryId = string.Empty;
                _popupQuantity = 0;
                _popupMaxQuantity = 0;
                _popupUnitPrice = 0;
            }

            if (binder.QuantityPopupRoot != null)
            {
                binder.QuantityPopupRoot.SetActive(visible);
            }
        }

        /// <summary>
        /// 软显隐（保持对象激活以持续监听事件）。
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

        /// <summary>
        /// 绑定按钮监听。
        /// </summary>
        private void HookButtons()
        {
            if (binder == null) return;

            if (binder.BuyButton != null) binder.BuyButton.onClick.AddListener(HandleBuyButtonClicked);
            if (binder.DecreaseButton != null) binder.DecreaseButton.onClick.AddListener(HandleDecreaseClicked);
            if (binder.IncreaseButton != null) binder.IncreaseButton.onClick.AddListener(HandleIncreaseClicked);
            if (binder.ConfirmButton != null) binder.ConfirmButton.onClick.AddListener(HandleConfirmPurchaseClicked);
            if (binder.CancelButton != null) binder.CancelButton.onClick.AddListener(HandleCancelPurchaseClicked);
        }

        /// <summary>
        /// 解绑按钮监听。
        /// </summary>
        private void UnhookButtons()
        {
            if (binder == null) return;

            if (binder.BuyButton != null) binder.BuyButton.onClick.RemoveListener(HandleBuyButtonClicked);
            if (binder.DecreaseButton != null) binder.DecreaseButton.onClick.RemoveListener(HandleDecreaseClicked);
            if (binder.IncreaseButton != null) binder.IncreaseButton.onClick.RemoveListener(HandleIncreaseClicked);
            if (binder.ConfirmButton != null) binder.ConfirmButton.onClick.RemoveListener(HandleConfirmPurchaseClicked);
            if (binder.CancelButton != null) binder.CancelButton.onClick.RemoveListener(HandleCancelPurchaseClicked);
        }

        /// <summary>
        /// 订阅 EventBus。
        /// </summary>
        private void SubscribeEvents()
        {
            EventBus.Subscribe<OpenShopUIRequestEvent>(HandleOpenShopUIRequest);
            EventBus.Subscribe<CloseShopUIRequestEvent>(HandleCloseShopUIRequest);
            EventBus.Subscribe<ShopItemSlotClickedEvent>(HandleShopItemSlotClicked);
            EventBus.Subscribe<GoldChangedEvent>(HandleGoldChangedEvent);
            EventBus.Subscribe<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompletedEvent);
        }

        /// <summary>
        /// 取消订阅 EventBus。
        /// </summary>
        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<OpenShopUIRequestEvent>(HandleOpenShopUIRequest);
            EventBus.Unsubscribe<CloseShopUIRequestEvent>(HandleCloseShopUIRequest);
            EventBus.Unsubscribe<ShopItemSlotClickedEvent>(HandleShopItemSlotClicked);
            EventBus.Unsubscribe<GoldChangedEvent>(HandleGoldChangedEvent);
            EventBus.Unsubscribe<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompletedEvent);
        }

        /// <summary>
        /// 订阅输入（ESC）。
        /// </summary>
        private void SubscribeInput()
        {
            if (inputReader == null) return;
            inputReader.UICancelEvent += HandleUICancel;
        }

        /// <summary>
        /// 取消订阅输入（ESC）。
        /// </summary>
        private void UnsubscribeInput()
        {
            if (inputReader == null) return;
            inputReader.UICancelEvent -= HandleUICancel;
        }

        /// <summary>
        /// 确保 CanvasGroup 可用。
        /// </summary>
        private void EnsureCanvasGroup()
        {
            _canvasGroup = binder != null && binder.CanvasGroup != null
                ? binder.CanvasGroup
                : GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 初始化对象池。
        /// </summary>
        private void EnsurePool()
        {
            if (_slotPool != null) return;
            if (binder == null || binder.SlotPrefab == null || binder.ListRoot == null) return;
            _slotPool = new GameObjectPool(binder.SlotPrefab, binder.ListRoot, slotPoolWarmup);
        }
    }
}
