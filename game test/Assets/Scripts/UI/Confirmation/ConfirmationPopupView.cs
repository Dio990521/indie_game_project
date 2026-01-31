using UnityEngine;
using IndieGame.Core;
using UnityEngine.Localization;

namespace IndieGame.UI.Confirmation
{
    /// <summary>
    /// 确认弹窗视图：
    /// 负责显示/隐藏弹窗、渲染文本、以及处理按钮点击与输入模式切换。
    /// </summary>
    public class ConfirmationPopupView : MonoBehaviour
    {
        [Header("Binder")]
        // 绑定器：集中 UI 引用
        [SerializeField] private ConfirmationPopupBinder binder;
        // 输入读取器：用于切换输入模式（UIOnly / Gameplay）
        [SerializeField] private IndieGame.Core.Input.GameInputReader inputReader;
        // 本地化“确认”按钮文本
        [SerializeField] private LocalizedString confirmLabel;
        // 本地化“取消”按钮文本
        [SerializeField] private LocalizedString cancelLabel;

        // 运行时数据容器
        private ConfirmationPopupData _data = new ConfirmationPopupData();
        // 可选的 CanvasGroup（用于软隐藏）
        private CanvasGroup _canvasGroup;
        // 是否使用 CanvasGroup 方式显示/隐藏
        private bool _useCanvasGroup = false;

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[ConfirmationPopupView] Missing binder reference.");
                return;
            }

            if (binder.ConfirmButton != null)
            {
                // 绑定确认按钮点击事件
                binder.ConfirmButton.onClick.AddListener(HandleConfirm);
            }

            if (binder.CancelButton != null)
            {
                // 绑定取消按钮点击事件
                binder.CancelButton.onClick.AddListener(HandleCancel);
            }

            // 初始化按钮文字与可见性
            ApplyButtonLabels();
            SetupVisibility();
            SetVisible(false);
        }

        private void Start()
        {
            if (binder != null)
            {
                Transform root = binder.RootPanel != null ? binder.RootPanel.transform : transform;
            }
        }

        private void OnEnable()
        {
            // 订阅确认请求事件
            EventBus.Subscribe<ConfirmationRequestEvent>(HandleRequest);
        }

        private void OnDisable()
        {
            // 退订事件，防止生命周期结束后仍被调用
            EventBus.Unsubscribe<ConfirmationRequestEvent>(HandleRequest);
        }

        private void HandleRequest(ConfirmationRequestEvent evt)
        {
            // 收到请求后刷新数据并显示弹窗
            _data.Message = evt.Request.Message;
            ApplyData();
            Show();
        }

        private void ApplyData()
        {
            if (binder.MessageLabel != null)
            {
                // 更新提示文本
                binder.MessageLabel.text = _data.Message;
            }
        }

        private void ApplyButtonLabels()
        {
            if (binder == null) return;
            ApplyButtonLabel(binder.ConfirmButton, confirmLabel);
            ApplyButtonLabel(binder.CancelButton, cancelLabel);
        }

        private void ApplyButtonLabel(UnityEngine.UI.Button button, LocalizedString text)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label == null) return;
            if (text == null)
            {
                label.text = string.Empty;
                return;
            }
            // 异步获取本地化文本
            var handle = text.GetLocalizedStringAsync();
            handle.Completed += op =>
            {
                if (label == null) return;
                label.text = op.Result;
            };
        }

        private void HandleConfirm()
        {
            // 确认后恢复输入模式
            if (inputReader != null) inputReader.SetInputMode(IndieGame.Core.Input.GameInputReader.InputMode.Gameplay);
            ConfirmationEvent.Respond(true);
            SetVisible(false);
        }

        private void HandleCancel()
        {
            // 取消后恢复输入模式
            if (inputReader != null) inputReader.SetInputMode(IndieGame.Core.Input.GameInputReader.InputMode.Gameplay);
            ConfirmationEvent.Respond(false);
            SetVisible(false);
        }

        private void SetupVisibility()
        {
            if (binder.RootPanel == null) return;
            if (binder.RootPanel == gameObject)
            {
                // 若根面板就是自身，则使用 CanvasGroup 控制显隐
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                _useCanvasGroup = true;
            }
        }

        private void SetVisible(bool visible)
        {
            if (binder.RootPanel == null) return;
            if (_useCanvasGroup && _canvasGroup != null)
            {
                // 软隐藏：保持对象活着但不可交互
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
                return;
            }

            // 硬隐藏：直接激活/禁用对象
            binder.RootPanel.SetActive(visible);
        }

        private void Show()
        {
            // 显示时切换到 UI 输入模式
            if (inputReader != null) inputReader.SetInputMode(IndieGame.Core.Input.GameInputReader.InputMode.UIOnly);
            SetVisible(true);
        }
    }
}
