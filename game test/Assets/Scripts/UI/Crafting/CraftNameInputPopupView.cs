using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IndieGame.Core;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 命名输入弹窗（简易实现）：
    ///
    /// 作用：
    /// - 监听 CraftNameInputPopupRequestEvent（打造命名），弹出输入框，确认后回传 CraftNameInputPopupResultEvent。
    /// - 同时监听 RenameSlotPopupRequestEvent（背包/强化界面的通用"改名"），确认后回传 RenameSlotPopupResultEvent。
    ///   两者语义不同（前者绑定"执行打造"，后者只是单纯改名字），但共用同一个输入框 UI，
    ///   通过 _pendingKind 记录当前弹窗是为哪种请求打开，确认/取消时回传对应的结果事件。
    ///
    /// 注意：
    /// - 该脚本不直接调用 CraftingSystem/InventoryManager，保持与业务逻辑解耦。
    /// - 由各自的 Controller 负责请求与响应匹配并决定后续动作。
    /// </summary>
    public class CraftNameInputPopupView : MonoBehaviour
    {
        private enum PendingRequestKind { None, Craft, Rename }

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
        // 当前弹窗是为哪种请求打开的，决定确认/取消时回传哪个结果事件
        private PendingRequestKind _pendingKind = PendingRequestKind.None;
        // 延迟一帧激活输入框的协程句柄（避免与上一次请求的延迟激活重叠）
        private Coroutine _activateNextFrameRoutine;

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
            EventBus.Subscribe<RenameSlotPopupRequestEvent>(HandleRenamePopupRequest);
            EventBus.Subscribe<CloseCraftingUIEvent>(HandleCraftingUIClose);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CraftNameInputPopupRequestEvent>(HandlePopupRequest);
            EventBus.Unsubscribe<RenameSlotPopupRequestEvent>(HandleRenamePopupRequest);
            EventBus.Unsubscribe<CloseCraftingUIEvent>(HandleCraftingUIClose);
        }

        /// <summary>
        /// 接收打造命名弹窗请求并展示：
        /// 输入框默认值来自请求中的 DefaultName。
        /// </summary>
        private void HandlePopupRequest(CraftNameInputPopupRequestEvent evt)
        {
            _pendingKind = PendingRequestKind.Craft;
            ShowPopup(evt.RequestId, evt.DefaultName);
        }

        /// <summary>
        /// 接收通用改名弹窗请求并展示。
        /// </summary>
        private void HandleRenamePopupRequest(RenameSlotPopupRequestEvent evt)
        {
            _pendingKind = PendingRequestKind.Rename;
            ShowPopup(evt.RequestId, evt.DefaultName);
        }

        private void ShowPopup(int requestId, string defaultName)
        {
            _currentRequestId = requestId;

            // 必须先激活面板再 ActivateInputField/Select：
            // 输入框此时仍挂在尚未激活的 rootPanel 下，EventSystem 无法选中一个未激活的物体，
            // 导致看似弹窗已打开但键盘输入实际未路由到输入框（只能用预填的默认名确认）。
            SetVisible(true);

            if (nameInputField != null)
                nameInputField.text = string.IsNullOrWhiteSpace(defaultName) ? string.Empty : defaultName;

            // 即使面板已激活，同一帧内调用 ActivateInputField 仍可能因为 EventSystem/Canvas
            // 尚未完成本帧的选中状态刷新而失效，因此延迟到下一帧再激活，确保稳定获得键盘焦点。
            if (_activateNextFrameRoutine != null) StopCoroutine(_activateNextFrameRoutine);
            _activateNextFrameRoutine = StartCoroutine(ActivateInputFieldNextFrame());
        }

        private IEnumerator ActivateInputFieldNextFrame()
        {
            yield return null;
            if (nameInputField != null)
            {
                nameInputField.ActivateInputField();
                nameInputField.Select();
            }
            _activateNextFrameRoutine = null;
        }

        /// <summary>
        /// 确认按钮：回传 confirmed=true 与输入文本（按 _pendingKind 回传对应事件类型）。
        /// </summary>
        private void HandleConfirmClicked()
        {
            if (_currentRequestId < 0) return;

            string input = nameInputField != null ? nameInputField.text : string.Empty;
            RaiseResult(confirmed: true, customName: input);

            _currentRequestId = -1;
            _pendingKind = PendingRequestKind.None;
            StopPendingActivation();
            SetVisible(false);
        }

        /// <summary>
        /// 取消按钮：回传 confirmed=false。
        /// </summary>
        private void HandleCancelClicked()
        {
            if (_currentRequestId < 0) return;

            RaiseResult(confirmed: false, customName: string.Empty);

            _currentRequestId = -1;
            _pendingKind = PendingRequestKind.None;
            StopPendingActivation();
            SetVisible(false);
        }

        private void RaiseResult(bool confirmed, string customName)
        {
            if (_pendingKind == PendingRequestKind.Rename)
            {
                EventBus.Raise(new RenameSlotPopupResultEvent
                {
                    RequestId = _currentRequestId,
                    Confirmed = confirmed,
                    CustomName = customName
                });
                return;
            }

            EventBus.Raise(new CraftNameInputPopupResultEvent
            {
                RequestId = _currentRequestId,
                Confirmed = confirmed,
                CustomName = customName
            });
        }

        /// <summary>
        /// 当 Craft UI 被关闭时，输入弹窗也应一并关闭并清空请求上下文。
        /// </summary>
        private void HandleCraftingUIClose(CloseCraftingUIEvent evt)
        {
            _currentRequestId = -1;
            _pendingKind = PendingRequestKind.None;
            StopPendingActivation();
            SetVisible(false);
        }

        private void StopPendingActivation()
        {
            if (_activateNextFrameRoutine == null) return;
            StopCoroutine(_activateNextFrameRoutine);
            _activateNextFrameRoutine = null;
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
