using UnityEngine;
using IndieGame.Core;
using UnityEngine.Localization;

namespace IndieGame.UI.Confirmation
{
    public class ConfirmationPopupView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private ConfirmationPopupBinder binder;
        [SerializeField] private IndieGame.Core.Input.GameInputReader inputReader;
        [SerializeField] private LocalizedString confirmLabel;
        [SerializeField] private LocalizedString cancelLabel;

        private ConfirmationPopupData _data = new ConfirmationPopupData();
        private CanvasGroup _canvasGroup;
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
                binder.ConfirmButton.onClick.AddListener(HandleConfirm);
            }

            if (binder.CancelButton != null)
            {
                binder.CancelButton.onClick.AddListener(HandleCancel);
            }

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
            EventBus.Subscribe<ConfirmationRequestEvent>(HandleRequest);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ConfirmationRequestEvent>(HandleRequest);
        }

        private void HandleRequest(ConfirmationRequestEvent evt)
        {
            _data.Message = evt.Request.Message;
            ApplyData();
            Show();
        }

        private void ApplyData()
        {
            if (binder.MessageLabel != null)
            {
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
            var handle = text.GetLocalizedStringAsync();
            handle.Completed += op =>
            {
                if (label == null) return;
                label.text = op.Result;
            };
        }

        private void HandleConfirm()
        {
            if (inputReader != null) inputReader.SetInputMode(IndieGame.Core.Input.GameInputReader.InputMode.Gameplay);
            ConfirmationEvent.Respond(true);
            SetVisible(false);
        }

        private void HandleCancel()
        {
            if (inputReader != null) inputReader.SetInputMode(IndieGame.Core.Input.GameInputReader.InputMode.Gameplay);
            ConfirmationEvent.Respond(false);
            SetVisible(false);
        }

        private void SetupVisibility()
        {
            if (binder.RootPanel == null) return;
            if (binder.RootPanel == gameObject)
            {
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
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
                return;
            }

            binder.RootPanel.SetActive(visible);
        }

        private void Show()
        {
            if (inputReader != null) inputReader.SetInputMode(IndieGame.Core.Input.GameInputReader.InputMode.UIOnly);
            SetVisible(true);
        }
    }
}
