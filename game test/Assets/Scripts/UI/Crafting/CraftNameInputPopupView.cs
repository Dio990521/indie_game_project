using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IndieGame.Core;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 成品命名输入弹窗（简易实现）：
    ///
    /// 作用：
    /// - 监听 CraftNameInputPopupRequestEvent，弹出输入框。
    /// - 点击确认/取消后，通过 CraftNameInputPopupResultEvent 回传结果。
    ///
    /// 注意：
    /// - 该脚本不直接调用 CraftingSystem，保持与业务逻辑解耦。
    /// - 由 CraftingUIController 负责请求与响应匹配并决定是否执行制造。
    /// </summary>
    public class CraftNameInputPopupView : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("弹窗根节点（用于显示/隐藏）")]
        [SerializeField] private GameObject rootPanel;
        [Tooltip("名称输入框")]
        [SerializeField] private TMP_InputField nameInputField;
        [Tooltip("确认按钮")]
        [SerializeField] private Button confirmButton;
        [Tooltip("取消按钮")]
        [SerializeField] private Button cancelButton;

        // 当前弹窗对应的请求 ID（用于响应事件回传）
        private int _currentRequestId = -1;

        private void Awake()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelClicked);
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CraftNameInputPopupRequestEvent>(HandlePopupRequest);
            EventBus.Subscribe<CloseCraftingUIEvent>(HandleCraftingUIClose);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CraftNameInputPopupRequestEvent>(HandlePopupRequest);
            EventBus.Unsubscribe<CloseCraftingUIEvent>(HandleCraftingUIClose);
        }

        /// <summary>
        /// 接收弹窗请求并展示：
        /// 输入框默认值来自请求中的 DefaultName。
        /// </summary>
        private void HandlePopupRequest(CraftNameInputPopupRequestEvent evt)
        {
            _currentRequestId = evt.RequestId;

            if (nameInputField != null)
            {
                nameInputField.text = string.IsNullOrWhiteSpace(evt.DefaultName) ? string.Empty : evt.DefaultName;
                nameInputField.ActivateInputField();
                nameInputField.Select();
            }

            SetVisible(true);
        }

        /// <summary>
        /// 确认按钮：回传 confirmed=true 与输入文本。
        /// </summary>
        private void HandleConfirmClicked()
        {
            if (_currentRequestId < 0) return;

            string input = nameInputField != null ? nameInputField.text : string.Empty;
            EventBus.Raise(new CraftNameInputPopupResultEvent
            {
                RequestId = _currentRequestId,
                Confirmed = true,
                CustomName = input
            });

            _currentRequestId = -1;
            SetVisible(false);
        }

        /// <summary>
        /// 取消按钮：回传 confirmed=false，不扣材料。
        /// </summary>
        private void HandleCancelClicked()
        {
            if (_currentRequestId < 0) return;

            EventBus.Raise(new CraftNameInputPopupResultEvent
            {
                RequestId = _currentRequestId,
                Confirmed = false,
                CustomName = string.Empty
            });

            _currentRequestId = -1;
            SetVisible(false);
        }

        /// <summary>
        /// 当 Craft UI 被关闭时，输入弹窗也应一并关闭并清空请求上下文。
        /// </summary>
        private void HandleCraftingUIClose(CloseCraftingUIEvent evt)
        {
            _currentRequestId = -1;
            SetVisible(false);
        }

        /// <summary>
        /// 弹窗显隐控制。
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (rootPanel != null)
            {
                rootPanel.SetActive(visible);
                return;
            }
            gameObject.SetActive(visible);
        }
    }
}
