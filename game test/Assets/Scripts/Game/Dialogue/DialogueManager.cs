using System;
using System.Collections;
using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 对话管理器（逻辑层，MonoSingleton）：
    /// 负责维护对话状态、处理交互输入、解析本地化文本、执行关键词高亮学习逻辑。
    ///
    /// 架构边界说明（MVB）：
    /// 1) 本类只处理“规则与状态”，不直接操作具体 UI 组件。
    /// 2) UI 显示/动画/打字机全部通过 EventBus 下发给 DialogueUIView 执行。
    /// 3) View 通过 EventBus 回传打字完成事件，Manager 再决定是否允许切下一句。
    ///
    /// 输入来源说明：
    /// - GameInputReader 在 OnInteract 中会广播 InputInteractEvent。
    /// - 本类只需订阅 InputInteractEvent，即可监听玩家交互键行为。
    /// </summary>
    public class DialogueManager : MonoSingleton<DialogueManager>
    {
        [Header("Config")]
        [Tooltip("打字机每个字符的默认间隔（秒）")]
        [SerializeField] private float defaultTypewriterSpeed = 0.03f;
        [Tooltip("开始对话时是否播放显示动画")]
        [SerializeField] private bool animateShow = true;
        [Tooltip("结束对话时是否播放隐藏动画")]
        [SerializeField] private bool animateHide = true;
        [Tooltip("对话刚启动后的输入保护时间（秒），避免同一次按键被误判为“下一句”")]
        [SerializeField] private float startInputBlockDuration = 0.08f;

        // 词条高亮颜色（富文本）
        private const string LearnableWordColorHex = "#FFD700";

        /// <summary>
        /// 当前是否处于对话流程中：
        /// true 代表对话 UI 处于可交互状态，并且 Interact 输入会被对话系统消费。
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 当前是否正在打字：
        /// true 代表本句文本尚未完整展示，再次按 Interact 会执行“跳过到全文”。
        /// </summary>
        public bool IsTyping { get; private set; }

        // 当前运行中的对话资源
        private DialogueDataSO _activeDialogue;
        // 当前行索引（从 0 开始）
        private int _currentLineIndex = -1;
        // 当前正在执行的“本地化解析 + 关键词高亮”协程
        private Coroutine _resolveLineCoroutine;
        // 对话启动后短暂输入屏蔽截止时间（用于避免同一帧二次消费 Interact）
        private float _blockInteractUntilTime;
        // 进入对话前的游戏状态快照（用于对话结束后恢复）
        private GameState _stateBeforeDialogue = GameState.FreeRoam;
        // 是否已记录过进入对话前的状态
        private bool _hasStateSnapshot;

        // 行解析器：负责本地化加载、关键词高亮、词条学习的协程逻辑（已抽离至独立类）
        private DialogueLineResolver _lineResolver;

        /// <summary>
        /// 对外入口：启动一段对话。
        /// </summary>
        public void StartDialogue(DialogueDataSO dialogueData)
        {
            if (dialogueData == null || dialogueData.Lines == null || dialogueData.Lines.Count == 0)
            {
                DebugTools.LogWarning("[DialogueManager] StartDialogue failed: DialogueData is null or empty.");
                return;
            }

            // 若已经在对话中，先结束旧对话但不隐藏 UI，避免“先淡出再淡入”造成闪烁。
            if (IsActive)
            {
                // 注意：连续切换对话时不恢复游戏状态，避免出现 Dialogue -> FreeRoam -> Dialogue 的抖动切换。
                InternalEndDialogue(
                    requestHide: false,
                    hideWithAnimation: false,
                    raiseEndedEvent: true,
                    restoreGameState: false);
            }

            _activeDialogue = dialogueData;
            _currentLineIndex = 0;
            IsActive = true;
            IsTyping = false;
            _blockInteractUntilTime = Time.unscaledTime + Mathf.Max(0f, startInputBlockDuration);

            // 对话状态切换策略：
            // 1) 记录进入对话前的状态，供结束时恢复。
            // 2) 当前是 FreeRoam 时切换到 Dialogue，从而让移动组件（如 SimpleMover）自动停止移动。
            CacheStateBeforeDialogueAndEnterDialogueState();

            EventBus.Raise(new DialogueStartedEvent
            {
                DialogueData = dialogueData
            });

            EventBus.Raise(new DialogueShowRequestEvent
            {
                Animate = animateShow
            });

            // 立即尝试展示首句。
            RequestPresentCurrentLine();
        }

        /// <summary>
        /// 对外入口：主动停止当前对话。
        /// </summary>
        public void StopDialogue()
        {
            if (!IsActive) return;
            InternalEndDialogue(
                requestHide: true,
                hideWithAnimation: animateHide,
                raiseEndedEvent: true,
                restoreGameState: true);
        }

        /// <summary>
        /// 查询词条是否已学习。
        /// </summary>
        public bool HasLearnedWord(string wordId)
        {
            EnsureResolver();
            return _lineResolver.HasLearnedWord(wordId);
        }

        /// <summary>
        /// 导出当前已学习词条 ID（只读拷贝）。
        /// </summary>
        public void GetLearnedWordIDs(List<string> output)
        {
            EnsureResolver();
            _lineResolver.GetLearnedWordIDs(output);
        }

        /// <summary>
        /// 懒初始化解析器：避免依赖 Awake 顺序。
        /// </summary>
        private void EnsureResolver()
        {
            if (_lineResolver == null)
            {
                _lineResolver = new DialogueLineResolver(defaultTypewriterSpeed, LearnableWordColorHex);
            }
        }

        private void OnEnable()
        {
            // 监听交互输入（GameInputReader -> EventBus -> DialogueManager）
            EventBus.Subscribe<InputInteractEvent>(HandleInteractInput);
            // 监听打字完成回调（DialogueUIView -> EventBus -> DialogueManager）
            EventBus.Subscribe<DialogueTypewriterCompletedEvent>(HandleTypewriterCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InputInteractEvent>(HandleInteractInput);
            EventBus.Unsubscribe<DialogueTypewriterCompletedEvent>(HandleTypewriterCompleted);

            StopResolveLineCoroutine();
            IsActive = false;
            IsTyping = false;
            _activeDialogue = null;
            _currentLineIndex = -1;
            _hasStateSnapshot = false;
        }

        /// <summary>
        /// 处理交互输入：
        /// 1) 正在打字 -> 跳过到完整文本
        /// 2) 已打完   -> 进入下一句；若已无下一句则结束对话
        /// </summary>
        private void HandleInteractInput(InputInteractEvent evt)
        {
            if (!IsActive) return;
            if (Time.unscaledTime < _blockInteractUntilTime) return;

            if (IsTyping)
            {
                EventBus.Raise(new DialogueSkipTypewriterRequestEvent());
                return;
            }

            MoveNextOrEnd();
        }

        /// <summary>
        /// 处理打字完成事件：
        /// 不论是自然完成还是 Skip 后瞬间完成，都在这里统一把 IsTyping 置为 false。
        /// </summary>
        private void HandleTypewriterCompleted(DialogueTypewriterCompletedEvent evt)
        {
            if (!IsActive) return;
            IsTyping = false;
        }

        /// <summary>
        /// 切到下一句；若越界则结束整个对话。
        /// </summary>
        private void MoveNextOrEnd()
        {
            if (_activeDialogue == null || _activeDialogue.Lines == null)
            {
                InternalEndDialogue(
                    requestHide: true,
                    hideWithAnimation: animateHide,
                    raiseEndedEvent: true,
                    restoreGameState: true);
                return;
            }

            _currentLineIndex++;
            if (_currentLineIndex >= _activeDialogue.Lines.Count)
            {
                InternalEndDialogue(
                    requestHide: true,
                    hideWithAnimation: animateHide,
                    raiseEndedEvent: true,
                    restoreGameState: true);
                return;
            }

            RequestPresentCurrentLine();
        }

        /// <summary>
        /// 请求展示“当前行”：
        /// 该过程会异步解析本地化文本，并在完成后交给 View 执行打字机。
        /// </summary>
        private void RequestPresentCurrentLine()
        {
            if (!IsActive || _activeDialogue == null || _activeDialogue.Lines == null)
            {
                return;
            }

            if (_currentLineIndex < 0 || _currentLineIndex >= _activeDialogue.Lines.Count)
            {
                InternalEndDialogue(
                    requestHide: true,
                    hideWithAnimation: animateHide,
                    raiseEndedEvent: true,
                    restoreGameState: true);
                return;
            }

            StopResolveLineCoroutine();
            EnsureResolver();

            // 捕获本次解析所属的 dialogue/lineIndex，用于异步完成时检验是否仍然有效
            DialogueDataSO expectedDialogue = _activeDialogue;
            int expectedLineIndex = _currentLineIndex;
            _resolveLineCoroutine = StartCoroutine(_lineResolver.ResolveAndPresentLineRoutine(
                expectedDialogue,
                expectedLineIndex,
                staleCheck: () => IsActive
                                  && _activeDialogue == expectedDialogue
                                  && _currentLineIndex == expectedLineIndex,
                onTypewriterStarting: () => { IsTyping = true; }
            ));
        }

        /// <summary>
        /// 结束当前对话并按参数决定是否隐藏 UI、是否广播结束事件、是否恢复游戏状态。
        /// </summary>
        private void InternalEndDialogue(bool requestHide, bool hideWithAnimation, bool raiseEndedEvent, bool restoreGameState)
        {
            DialogueDataSO endedDialogue = _activeDialogue;

            StopResolveLineCoroutine();

            IsActive = false;
            IsTyping = false;
            _currentLineIndex = -1;
            _activeDialogue = null;

            if (restoreGameState)
            {
                RestoreStateAfterDialogueIfNeeded();
            }

            if (requestHide)
            {
                EventBus.Raise(new DialogueHideRequestEvent
                {
                    Animate = hideWithAnimation
                });
            }

            if (raiseEndedEvent && endedDialogue != null)
            {
                EventBus.Raise(new DialogueEndedEvent
                {
                    DialogueData = endedDialogue
                });
            }
        }

        /// <summary>
        /// 记录进入对话前状态，并在需要时切换到 Dialogue。
        /// </summary>
        private void CacheStateBeforeDialogueAndEnterDialogueState()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null) return;

            _stateBeforeDialogue = gameManager.CurrentState;
            _hasStateSnapshot = true;

            // 用户目标：对话期间禁止角色自由移动。
            // 这里仅在 FreeRoam 下切到 Dialogue，避免影响其它流程状态机（如加载中/棋盘中）。
            if (gameManager.CurrentState == GameState.FreeRoam)
            {
                gameManager.ChangeState(GameState.Dialogue);
            }
        }

        /// <summary>
        /// 对话结束后的状态恢复：
        /// - 若当前仍是 Dialogue，恢复到进入对话前的状态；
        /// - 若进入前本来就是 Dialogue（理论上极少），回退到 FreeRoam 作为安全兜底。
        /// </summary>
        private void RestoreStateAfterDialogueIfNeeded()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                _hasStateSnapshot = false;
                return;
            }

            if (!_hasStateSnapshot)
            {
                // 没有快照时不做强制恢复，避免误改外部状态。
                return;
            }

            if (gameManager.CurrentState == GameState.Dialogue)
            {
                GameState restoreState = _stateBeforeDialogue == GameState.Dialogue
                    ? GameState.FreeRoam
                    : _stateBeforeDialogue;
                gameManager.ChangeState(restoreState);
            }

            _hasStateSnapshot = false;
        }

        /// <summary>
        /// 安全停止当前“行解析协程”。
        /// </summary>
        private void StopResolveLineCoroutine()
        {
            if (_resolveLineCoroutine == null) return;
            StopCoroutine(_resolveLineCoroutine);
            _resolveLineCoroutine = null;
        }
    }
}
