using System;
using System.Collections;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 对话行解析器（纯 C# 辅助类，无 MonoBehaviour 生命周期）：
    /// 把 DialogueManager 中最复杂的“本地化解析 + 打字机发起”
    /// 抽离到这里，让 DialogueManager 只专注于流程控制。
    ///
    /// 由 DialogueManager 在构造时实例化并调用 <see cref="ResolveAndPresentLineRoutine"/>。
    ///
    /// 关键设计：
    /// - 通过 <c>staleCheck</c> 回调让外部告诉我们“当前这次解析是否还有效”；
    ///   异步加载到一半若 Manager 已切到下一句/结束对话，本协程会丢弃结果而非脏刷新。
    /// - 通过 <c>onTypewriterStarting</c> 回调在发起打字机事件前同步外部状态（如 IsTyping=true）。
    /// </summary>
    internal class DialogueLineResolver
    {
        private readonly float _typewriterSpeed;

        public DialogueLineResolver(float typewriterSpeed)
        {
            _typewriterSpeed = Mathf.Max(0.001f, typewriterSpeed);
        }

        /// <summary>
        /// 协程：解析当前行并下发到 View。
        /// 关键流程：
        /// 1) 异步获取说话人与正文的本地化字符串；
        /// 2) 通过 staleCheck 校验“是否仍然是当前应展示的行”；
        /// 3) 调用 onTypewriterStarting，再下发说话人/打字机事件。
        /// </summary>
        public IEnumerator ResolveAndPresentLineRoutine(
            DialogueDataSO dialogue,
            int lineIndex,
            Func<bool> staleCheck,
            Action onTypewriterStarting)
        {
            if (dialogue == null || dialogue.Lines == null) yield break;
            if (lineIndex < 0 || lineIndex >= dialogue.Lines.Count) yield break;

            DialogueLine line = dialogue.Lines[lineIndex];
            if (line == null)
            {
                // 空行兜底：直接视为空文本句子，允许玩家继续下一句。
                EventBus.Raise(new DialogueSpeakerChangedEvent { SpeakerText = string.Empty });
                onTypewriterStarting?.Invoke();
                EventBus.Raise(new DialogueTypewriterRequestEvent
                {
                    FormattedText = string.Empty,
                    Speed = _typewriterSpeed
                });
                yield break;
            }

            // --- 1) 异步解析说话人与正文 ---
            string speakerText = string.Empty;
            if (line.Speaker != null)
            {
                var speakerHandle = line.Speaker.GetLocalizedStringAsync();
                yield return speakerHandle;
                speakerText = speakerHandle.Result ?? string.Empty;
            }

            string contentText = string.Empty;
            if (line.Content != null)
            {
                var contentHandle = line.Content.GetLocalizedStringAsync();
                yield return contentHandle;
                contentText = contentHandle.Result ?? string.Empty;
            }

            // --- 2) 防并发串线：异步过程中若已切句/结束对话，丢弃结果 ---
            if (staleCheck != null && !staleCheck()) yield break;

            // --- 3) 下发到 View ---
            EventBus.Raise(new DialogueSpeakerChangedEvent { SpeakerText = speakerText });
            onTypewriterStarting?.Invoke();
            EventBus.Raise(new DialogueTypewriterRequestEvent
            {
                FormattedText = contentText,
                Speed = _typewriterSpeed
            });
        }
    }
}
