using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Shop;

namespace IndieGame.UI.Shop
{
    /// <summary>
    /// 商店数量弹窗控制器（纯 C# 辅助类）：
    /// 职责：封装数量弹窗的所有状态变量、显隐逻辑、视觉刷新、确认/取消处理。
    /// 由 ShopUIController 在 Awake 中实例化并持有。
    ///
    /// ESC 优先级由 ShopUIController 控制：先关弹窗，再关商店。
    /// </summary>
    internal class ShopQuantityPopupController
    {
        private ShopUIBinder _binder;

        private bool _isOpen;
        private string _popupShopId;
        private string _popupShopEntryId;
        private int _popupQuantity;
        private int _popupMaxQuantity;
        private int _popupUnitPrice;

        public bool IsOpen => _isOpen;

        /// <summary>
        /// 初始化：绑定 UI 引用，在 Awake 中调用。
        /// </summary>
        public void Init(ShopUIBinder binder)
        {
            _binder = binder;
        }

        /// <summary>
        /// 打开弹窗并初始化数量选择上下文。
        /// </summary>
        public void Open(string shopId, ShopListManager.ShopListEntry entry, int maxPurchasable)
        {
            _popupShopId      = shopId;
            _popupShopEntryId = entry.ShopEntryID;
            _popupUnitPrice   = entry.UnitPrice;
            _popupMaxQuantity = maxPurchasable;
            _popupQuantity    = Mathf.Clamp(1, 1, _popupMaxQuantity);

            SetPopupVisible(true);
            RefreshVisual();
        }

        /// <summary>
        /// 关闭弹窗并重置所有状态。
        /// </summary>
        public void Close()
        {
            _popupShopId      = string.Empty;
            _popupShopEntryId = string.Empty;
            _popupQuantity    = 0;
            _popupMaxQuantity = 0;
            _popupUnitPrice   = 0;

            SetPopupVisible(false);
        }

        /// <summary>
        /// 减少数量按钮处理。
        /// </summary>
        public void HandleDecrease()
        {
            if (!_isOpen) return;
            _popupQuantity = Mathf.Max(1, _popupQuantity - 1);
            RefreshVisual();
        }

        /// <summary>
        /// 增加数量按钮处理。
        /// </summary>
        public void HandleIncrease()
        {
            if (!_isOpen) return;
            _popupQuantity = Mathf.Min(_popupMaxQuantity, _popupQuantity + 1);
            RefreshVisual();
        }

        /// <summary>
        /// 确认购买：调用 ShopSystem.TryPurchase，成功后关闭弹窗。
        /// 返回购买是否成功（失败时由 Controller 决定是否刷新界面）。
        /// </summary>
        public bool TryConfirm()
        {
            if (!_isOpen || string.IsNullOrWhiteSpace(_popupShopId) || string.IsNullOrWhiteSpace(_popupShopEntryId))
                return false;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null) return false;

            ShopPurchaseResult result = shopSystem.TryPurchase(_popupShopId, _popupShopEntryId, _popupQuantity);
            if (!result.Success)
            {
                DebugTools.LogWarning($"[ShopQuantityPopupController] Purchase failed: {result.FailReason} - {result.Message}");
                return false;
            }

            // 成功后关闭弹窗；列表刷新由 ShopPurchaseCompletedEvent 驱动
            Close();
            return true;
        }

        /// <summary>
        /// 外部状态变化时（金币/库存）重新计算上限并刷新。
        /// 若上限归零则自动关闭弹窗。
        /// </summary>
        public void RefreshState(string shopId, ShopListManager.ShopListEntry selected)
        {
            if (!_isOpen) return;

            ShopSystem shopSystem = ShopSystem.Instance;
            if (shopSystem == null) { Close(); return; }

            _popupMaxQuantity = shopSystem.GetMaxPurchasableQuantity(selected.ShopID, selected.ShopEntryID);
            _popupUnitPrice   = selected.UnitPrice;
            _popupShopEntryId = selected.ShopEntryID;

            if (_popupMaxQuantity <= 0) { Close(); return; }

            _popupQuantity = Mathf.Clamp(_popupQuantity, 1, _popupMaxQuantity);
            RefreshVisual();
        }

        // --- 私有方法 ---

        private void SetPopupVisible(bool visible)
        {
            _isOpen = visible;
            if (_binder.QuantityPopupRoot != null)
                _binder.QuantityPopupRoot.SetActive(visible);
        }

        private void RefreshVisual()
        {
            if (!_isOpen) return;

            if (_binder.QuantityValueText != null)
                _binder.QuantityValueText.text = _popupQuantity.ToString();

            if (_binder.TotalPriceValueText != null)
            {
                long total = (long)_popupUnitPrice * _popupQuantity;
                _binder.TotalPriceValueText.text = $"{total} G";
            }

            if (_binder.DecreaseButton != null)
                _binder.DecreaseButton.interactable = _popupQuantity > 1;

            if (_binder.IncreaseButton != null)
                _binder.IncreaseButton.interactable = _popupQuantity < _popupMaxQuantity;

            if (_binder.ConfirmButton != null)
                _binder.ConfirmButton.interactable = _popupMaxQuantity > 0;
        }
    }
}
