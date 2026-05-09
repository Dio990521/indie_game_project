using System;
using TMPro;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Economy;
using IndieGame.Gameplay.Shop;

namespace IndieGame.UI.Shop
{
    /// <summary>
    /// 商店界面协调器（薄控制层）：
    /// 负责界面显隐、游戏状态快照管理、EventBus订阅与输入处理。
    ///
    /// 架构边界：
    /// - ShopUIBinder：只保存引用，不写业务逻辑。
    /// - ShopListManager：管理左侧商品列表对象池与条目索引。
    /// - ShopQuantityPopupController：封装数量弹窗状态与确认/取消流程。
    /// - ShopUIController（本类）：协调上述两者，处理生命周期与事件路由。
    /// - ShopSystem：只负责商业规则（库存、限购、扣款）。
    ///
    /// ESC 优先级：先关数量弹窗，再关整个商店界面。
    /// </summary>
    public class ShopUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ShopUIBinder binder;
        [SerializeField] private GameInputReader inputReader;

        [Header("Pool Settings")]
        [Tooltip("商品列表对象池预热数量。")]
        [SerializeField] private int slotPoolWarmup = 12;

        private ShopListManager _listManager;
        private ShopQuantityPopupController _quantityPopup;

        private CanvasGroup _canvasGroup;
        private bool _isVisible;
        private string _currentShopId;

        // 进入商店前的状态快照（关闭时用于恢复）
        private bool _hasStateSnapshot;
        private GameState _stateBeforeShop = GameState.FreeRoam;

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[ShopUIController] Missing ShopUIBinder reference.");
                return;
            }

            _listManager   = new ShopListManager();
            _quantityPopup = new ShopQuantityPopupController();
            _listManager.Init(binder, slotPoolWarmup);
            _quantityPopup.Init(binder);

            EnsureCanvasGroup();
            HookButtons();
            SetVisible(false);
            _quantityPopup.Close();
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
            _quantityPopup.Close();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            UnsubscribeInput();
            _listManager.ReleaseAll();
            ClearSelectionAndDetail();
            _quantityPopup.Close();
            // 保险恢复：场景切换等意外禁用时，避免玩家被卡在 Paused
            RestoreStateAfterShopIfNeeded();
        }

        // --- 事件处理 ---

        private void HandleOpenShopUIRequest(OpenShopUIRequestEvent evt)
        {
            if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(evt.ShopID)) return;

            _currentShopId = evt.ShopID.Trim();
            CacheStateBeforeShopAndEnterPaused();
            RebuildList(preferredShopEntryId: null);
            RefreshGoldDisplay();
            SetVisible(true);
        }

        private void HandleCloseShopUIRequest(CloseShopUIRequestEvent evt)
        {
            if (!isActiveAndEnabled || !_isVisible) return;

            _quantityPopup.Close();
            _listManager.ReleaseAll();
            ClearSelectionAndDetail();
            SetVisible(false);
            RestoreStateAfterShopIfNeeded();
        }

        private void HandleShopItemSlotClicked(ShopItemSlotClickedEvent evt)
        {
            if (!_isVisible) return;
            if (!string.Equals(evt.ShopID, _currentShopId, StringComparison.Ordinal)) return;
            if (string.IsNullOrWhiteSpace(evt.EntryKey)) return;
            if (!_listManager.TryGetEntry(evt.EntryKey, out _)) return;

            OnEntrySelected(evt.EntryKey);
        }

        private void HandleGoldChangedEvent(GoldChangedEvent evt)
        {
            if (!_isVisible) return;
            RefreshGoldDisplay();
            RefreshBuyButtonState();

            // 弹窗打开时实时更新可购买上限
            if (_quantityPopup.IsOpen && _listManager.TryGetSelectedEntry(out ShopListManager.ShopListEntry selected))
                _quantityPopup.RefreshState(_currentShopId, selected);
        }

        private void HandleShopPurchaseCompletedEvent(ShopPurchaseCompletedEvent evt)
        {
            if (!_isVisible) return;
            if (!string.Equals(evt.ShopID, _currentShopId, StringComparison.Ordinal)) return;

            RebuildList(preferredShopEntryId: evt.ShopEntryID);
            RefreshGoldDisplay();
        }

        // --- 按钮回调 ---

        private void HandleBuyButtonClicked()
        {
            if (!_isVisible || !_listManager.TryGetSelectedEntry(out ShopListManager.ShopListEntry selected)) return;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null) return;

            int maxPurchasable = shopSystem.GetMaxPurchasableQuantity(selected.ShopID, selected.ShopEntryID);
            if (maxPurchasable <= 0) { RefreshBuyButtonState(); return; }

            _quantityPopup.Open(_currentShopId, selected, maxPurchasable);
        }

        private void HandleDecreaseClicked()   => _quantityPopup.HandleDecrease();
        private void HandleIncreaseClicked()   => _quantityPopup.HandleIncrease();
        private void HandleCancelPurchaseClicked() => _quantityPopup.Close();

        private void HandleConfirmPurchaseClicked()
        {
            bool success = _quantityPopup.TryConfirm();
            if (!success)
            {
                // 购买失败时刷新按钮和弹窗状态，不关闭弹窗
                RefreshBuyButtonState();
                if (_quantityPopup.IsOpen && _listManager.TryGetSelectedEntry(out ShopListManager.ShopListEntry sel))
                    _quantityPopup.RefreshState(_currentShopId, sel);
            }
        }

        private void HandleUICancel()
        {
            if (!_isVisible) return;
            if (_quantityPopup.IsOpen) { _quantityPopup.Close(); return; }
            EventBus.Raise(new CloseShopUIRequestEvent());
        }

        // --- 列表与详情 ---

        private void RebuildList(string preferredShopEntryId)
        {
            ShopSystem shopSystem = ShopSystem.Instance;
            bool hasEntries = _listManager.RebuildList(_currentShopId, shopSystem);

            if (!hasEntries) { EnterEmptyState(); return; }

            string targetKey = _listManager.FindEntryKeyByShopEntryId(preferredShopEntryId);
            if (string.IsNullOrWhiteSpace(targetKey))
                targetKey = _listManager.EntryOrder[0];

            OnEntrySelected(targetKey);
        }

        private void OnEntrySelected(string entryKey)
        {
            if (!_listManager.TryGetEntry(entryKey, out ShopListManager.ShopListEntry entry))
            {
                EnterEmptyState();
                return;
            }

            _listManager.Select(entryKey);

            if (binder.EmptyStateNode != null)
                binder.EmptyStateNode.SetActive(false);

            RefreshDetailText(entry);
            RefreshBuyButtonState();
        }

        private void RefreshDetailText(ShopListManager.ShopListEntry entry)
        {
            TMP_Text text = binder.DescriptionText;
            if (text == null) return;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null) { text.text = entry.Description ?? string.Empty; return; }

            int stock = shopSystem.GetRemainingStock(entry.ShopID, entry.ShopEntryID);
            int quota = shopSystem.GetRemainingPurchaseQuota(entry.ShopID, entry.ShopEntryID);

            string stockText = stock < 0 ? "Stock: Unlimited" : $"Stock: {stock}";
            string quotaText = quota < 0 ? "Purchase Quota: Unlimited" : $"Remaining Purchase Quota: {quota}";

            text.text = $"{entry.Description}\n\nUnit Price: {entry.UnitPrice} G\n{stockText}\n{quotaText}";
        }

        private void RefreshBuyButtonState()
        {
            if (binder.BuyButton == null) return;
            if (!_listManager.TryGetSelectedEntry(out ShopListManager.ShopListEntry selected))
            {
                binder.BuyButton.interactable = false;
                return;
            }

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null) { binder.BuyButton.interactable = false; return; }

            int max = shopSystem.GetMaxPurchasableQuantity(selected.ShopID, selected.ShopEntryID);
            binder.BuyButton.interactable = max > 0;
        }

        private void RefreshGoldDisplay()
        {
            if (binder.GoldValueText == null) return;
            GoldSystem goldSystem = GoldSystem.Instance;
            int currentGold = goldSystem != null ? goldSystem.CurrentGold : 0;
            binder.GoldValueText.text = $"Gold: {currentGold}";
        }

        private void EnterEmptyState()
        {
            if (binder.EmptyStateNode != null) binder.EmptyStateNode.SetActive(true);
            if (binder.DescriptionText != null) binder.DescriptionText.text = "No purchasable items available.";
            if (binder.BuyButton != null) binder.BuyButton.interactable = false;
            _quantityPopup.Close();
        }

        private void ClearSelectionAndDetail()
        {
            _currentShopId = string.Empty;
            if (binder.DescriptionText != null) binder.DescriptionText.text = string.Empty;
            if (binder.BuyButton != null) binder.BuyButton.interactable = false;
            if (binder.GoldValueText != null) binder.GoldValueText.text = "Gold: 0";
        }

        // --- 游戏状态快照（进/退商店时保存/恢复玩家状态）---

        private void CacheStateBeforeShopAndEnterPaused()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            _stateBeforeShop  = gameManager.CurrentState;
            _hasStateSnapshot = true;

            if (gameManager.CurrentState == GameState.FreeRoam)
                gameManager.ChangeState(GameState.Paused);
        }

        private void RestoreStateAfterShopIfNeeded()
        {
            if (!_hasStateSnapshot) return;

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) { _hasStateSnapshot = false; return; }

            if (gameManager.CurrentState == GameState.Paused)
            {
                GameState restoreState = _stateBeforeShop == GameState.Paused
                    ? GameState.FreeRoam
                    : _stateBeforeShop;
                gameManager.ChangeState(restoreState);
            }

            _hasStateSnapshot = false;
        }

        // --- 显隐 ---

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup == null) EnsureCanvasGroup();
            if (_canvasGroup == null) return;
            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable   = visible;
        }

        // --- 初始化工具 ---

        private void EnsureCanvasGroup()
        {
            _canvasGroup = binder != null && binder.CanvasGroup != null
                ? binder.CanvasGroup
                : GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void HookButtons()
        {
            if (binder == null) return;
            if (binder.BuyButton != null)      binder.BuyButton.onClick.AddListener(HandleBuyButtonClicked);
            if (binder.DecreaseButton != null) binder.DecreaseButton.onClick.AddListener(HandleDecreaseClicked);
            if (binder.IncreaseButton != null) binder.IncreaseButton.onClick.AddListener(HandleIncreaseClicked);
            if (binder.ConfirmButton != null)  binder.ConfirmButton.onClick.AddListener(HandleConfirmPurchaseClicked);
            if (binder.CancelButton != null)   binder.CancelButton.onClick.AddListener(HandleCancelPurchaseClicked);
        }

        private void UnhookButtons()
        {
            if (binder == null) return;
            if (binder.BuyButton != null)      binder.BuyButton.onClick.RemoveListener(HandleBuyButtonClicked);
            if (binder.DecreaseButton != null) binder.DecreaseButton.onClick.RemoveListener(HandleDecreaseClicked);
            if (binder.IncreaseButton != null) binder.IncreaseButton.onClick.RemoveListener(HandleIncreaseClicked);
            if (binder.ConfirmButton != null)  binder.ConfirmButton.onClick.RemoveListener(HandleConfirmPurchaseClicked);
            if (binder.CancelButton != null)   binder.CancelButton.onClick.RemoveListener(HandleCancelPurchaseClicked);
        }

        // --- 订阅管理 ---

        private void SubscribeEvents()
        {
            EventBus.Subscribe<OpenShopUIRequestEvent>(HandleOpenShopUIRequest);
            EventBus.Subscribe<CloseShopUIRequestEvent>(HandleCloseShopUIRequest);
            EventBus.Subscribe<ShopItemSlotClickedEvent>(HandleShopItemSlotClicked);
            EventBus.Subscribe<GoldChangedEvent>(HandleGoldChangedEvent);
            EventBus.Subscribe<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompletedEvent);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<OpenShopUIRequestEvent>(HandleOpenShopUIRequest);
            EventBus.Unsubscribe<CloseShopUIRequestEvent>(HandleCloseShopUIRequest);
            EventBus.Unsubscribe<ShopItemSlotClickedEvent>(HandleShopItemSlotClicked);
            EventBus.Unsubscribe<GoldChangedEvent>(HandleGoldChangedEvent);
            EventBus.Unsubscribe<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompletedEvent);
        }

        private void SubscribeInput()
        {
            if (inputReader != null) inputReader.UICancelEvent += HandleUICancel;
        }

        private void UnsubscribeInput()
        {
            if (inputReader != null) inputReader.UICancelEvent -= HandleUICancel;
        }
    }
}
