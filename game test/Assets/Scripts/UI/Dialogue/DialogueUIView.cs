using System.Collections;
using DG.Tweening;
using IndieGame.Core;
using TMPro;
using UnityEngine;

namespace IndieGame.UI.Dialogue
{
    /// <summary>
    /// 对话 UI 视图层（MVB-View）：
    /// 负责三类职责：
    /// 1) 监听 EventBus 的 UI 指令事件（显示/隐藏/文本刷新/跳过）。
    /// 2) 执行 DOTween 淡入淡出动画。
    /// 3) 执行打字机逐字显示，并在结束后回发完成事件。
    ///
    /// 注意：
    /// - View 只处理“怎么显示”，不处理“何时切下一句”等业务决策。
    /// - 业务决策由 DialogueManager 统一管理。
    /// </summary>
    public class DialogueUIView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private DialogueUIBinder binder;

        [Header("Animation")]
        [Tooltip("显示动画时长（秒）")]
        [SerializeField] private float showDuration = 0.2f;
        [Tooltip("隐藏动画时长（秒）")]
        [SerializeField] private float hideDuration = 0.2f;

        // 运行时缓存：实际用于透明度与交互控制的 CanvasGroup
        private CanvasGroup _canvasGroup;
        // 运行时缓存：对话容器根节点（用于硬开关）
        private GameObject _container;
        // 当前打字机协程
        private Coroutine _typewriterCoroutine;
        // 当前句子的完整富文本（用于 Skip 时直接全量显示）
        private string _fullFormattedText = string.Empty;
        // 当前是否仍处于“打字进行中”
        private bool _isTypewriterRunning;

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[DialogueUIView] Missing DialogueUIBinder reference.");
                return;
            }

            _container = binder.DialogueContainer != null ? binder.DialogueContainer : gameObject;
            _canvasGroup = binder.CanvasGroup != null ? binder.CanvasGroup : _container.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                // 兜底：若未配置 CanvasGroup，运行时自动补一个，确保 Show/Hide 可工作。
                _canvasGroup = _container.AddComponent<CanvasGroup>();
            }

            // 启动时默认隐藏，避免场景加载第一帧出现闪烁。
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _container.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueShowRequestEvent>(HandleShowRequest);
            EventBus.Subscribe<DialogueHideRequestEvent>(HandleHideRequest);
            EventBus.Subscribe<DialogueSpeakerChangedEvent>(HandleSpeakerChanged);
            EventBus.Subscribe<DialogueTypewriterRequestEvent>(HandleTypewriterRequest);
            EventBus.Subscribe<DialogueSkipTypewriterRequestEvent>(HandleSkipTypewriterRequest);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueShowRequestEvent>(HandleShowRequest);
            EventBus.Unsubscribe<DialogueHideRequestEvent>(HandleHideRequest);
            EventBus.Unsubscribe<DialogueSpeakerChangedEvent>(HandleSpeakerChanged);
            EventBus.Unsubscribe<DialogueTypewriterRequestEvent>(HandleTypewriterRequest);
            EventBus.Unsubscribe<DialogueSkipTypewriterRequestEvent>(HandleSkipTypewriterRequest);

            _canvasGroup?.DOKill();
            StopTypewriterCoroutineOnly();
            _isTypewriterRunning = false;
        }

        /// <summary>
        /// 显示对话 UI。
        /// </summary>
        /// <param name="animate">true=播放淡入，false=立即显示</param>
        public void Show(bool animate)
        {
            if (_canvasGroup == null || _container == null) return;

            _container.SetActive(true);
            _canvasGroup.DOKill();
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            if (!animate)
            {
                _canvasGroup.alpha = 1f;
                return;
            }

            // 淡入前强制从 0 开始，可避免上一次中断动画残留导致的闪屏。
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, Mathf.Max(0f, showDuration));
        }

        /// <summary>
        /// 隐藏对话 UI。
        /// </summary>
        /// <param name="animate">true=播放淡出，false=立即隐藏</param>
        public void Hide(bool animate)
        {
            if (_canvasGroup == null || _container == null) return;

            _canvasGroup.DOKill();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // 隐藏时直接中止打字协程，避免对象不可见后仍在后台逐字刷新。
            StopTypewriterCoroutineOnly();
            _isTypewriterRunning = false;

            if (!animate)
            {
                _canvasGroup.alpha = 0f;
                _container.SetActive(false);
                return;
            }

            _canvasGroup.DOFade(0f, Mathf.Max(0f, hideDuration)).OnComplete(() =>
            {
                if (_container != null)
                {
                    _container.SetActive(false);
                }
            });
        }

        /// <summary>
        /// 启动打字机效果。
        /// </summary>
        /// <param name="formattedText">已完成富文本高亮处理的文本</param>
        /// <param name="speed">逐字间隔（秒），小于等于 0 表示立即显示全文</param>
        public void StartTypewriter(string formattedText, float speed)
        {
            TMP_Text contentText = binder != null ? binder.ContentValText : null;
            if (contentText == null)
            {
                return;
            }

            StopTypewriterCoroutineOnly();

            _fullFormattedText = formattedText ?? string.Empty;
            contentText.text = _fullFormattedText;

            // 对于空文本或非法速度，直接显示全文并立即回发“打字完成”事件。
            if (string.IsNullOrEmpty(_fullFormattedText) || speed <= 0f)
            {
                contentText.maxVisibleCharacters = int.MaxValue;
                _isTypewriterRunning = true;
                CompleteTypewriterIfNeeded();
                return;
            }

            _isTypewriterRunning = true;
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(Mathf.Max(0.001f, speed)));
        }

        /// <summary>
        /// 跳过打字机并直接显示完整文本。
        /// </summary>
        public void StopTypewriterAndShowFull()
        {
            TMP_Text contentText = binder != null ? binder.ContentValText : null;
            if (contentText == null) return;

            StopTypewriterCoroutineOnly();
            contentText.text = _fullFormattedText;
            contentText.maxVisibleCharacters = int.MaxValue;
            CompleteTypewriterIfNeeded();
        }

        private IEnumerator TypewriterRoutine(float perCharacterDelay)
        {
            TMP_Text contentText = binder != null ? binder.ContentValText : null;
            if (contentText == null)
            {
                CompleteTypewriterIfNeeded();
                yield break;
            }

            // 用 TMP 的 maxVisibleCharacters 实现逐字显示，可正确兼容富文本标签。
            contentText.maxVisibleCharacters = 0;
            contentText.ForceMeshUpdate();
            int totalVisibleCount = contentText.textInfo.characterCount;

            if (totalVisibleCount <= 0)
            {
                CompleteTypewriterIfNeeded();
                yield break;
            }

            for (int i = 1; i <= totalVisibleCount; i++)
            {
                // 如果外部已中止（例如收到 Skip），立即退出协程。
                if (!_isTypewriterRunning) yield break;

                contentText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(perCharacterDelay);
            }

            // 自然播放到末尾后，回发完成事件。
            CompleteTypewriterIfNeeded();
        }

        /// <summary>
        /// 仅停止协程，不触发“完成事件”。
        /// </summary>
        private void StopTypewriterCoroutineOnly()
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
        }

        /// <summary>
        /// 结束打字阶段并回发事件（只会触发一次）。
        /// </summary>
        private void CompleteTypewriterIfNeeded()
        {
            if (!_isTypewriterRunning) return;
            _isTypewriterRunning = false;
            EventBus.Raise(new DialogueTypewriterCompletedEvent());
        }

        private void HandleShowRequest(DialogueShowRequestEvent evt)
        {
            Show(evt.Animate);
        }

        private void HandleHideRequest(DialogueHideRequestEvent evt)
        {
            Hide(evt.Animate);
        }

        private void HandleSpeakerChanged(DialogueSpeakerChangedEvent evt)
        {
            TMP_Text nameText = binder != null ? binder.NameValText : null;
            if (nameText == null) return;
            nameText.text = evt.SpeakerText ?? string.Empty;
        }

        private void HandleTypewriterRequest(DialogueTypewriterRequestEvent evt)
        {
            StartTypewriter(evt.FormattedText, evt.Speed);
        }

        private void HandleSkipTypewriterRequest(DialogueSkipTypewriterRequestEvent evt)
        {
            StopTypewriterAndShowFull();
        }
    }
}
