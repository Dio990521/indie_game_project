using System;
using System.Collections;
using System.Collections.Generic;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 对话行解析器（纯 C# 辅助类，无 MonoBehaviour 生命周期）：
    /// 把 DialogueManager 中最复杂的“本地化解析 + 关键词高亮 + 词条学习 + 打字机发起”
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
        private readonly string _highlightColorHex;
        // 已学习词条集合（运行时去重容器）
        private readonly HashSet<string> _learnedWordIds = new HashSet<string>(StringComparer.Ordinal);

        public DialogueLineResolver(float typewriterSpeed, string highlightColorHex)
        {
            _typewriterSpeed = Mathf.Max(0.001f, typewriterSpeed);
            _highlightColorHex = string.IsNullOrWhiteSpace(highlightColorHex) ? "#FFD700" : highlightColorHex;
        }

        /// <summary>
        /// 查询词条是否已学习。
        /// </summary>
        public bool HasLearnedWord(string wordId)
        {
            if (string.IsNullOrWhiteSpace(wordId)) return false;
            return _learnedWordIds.Contains(wordId.Trim());
        }

        /// <summary>
        /// 导出当前已学习词条 ID 到外部 List（避免分配新集合）。
        /// </summary>
        public void GetLearnedWordIDs(List<string> output)
        {
            if (output == null) return;
            output.Clear();
            foreach (string id in _learnedWordIds)
            {
                output.Add(id);
            }
        }

        /// <summary>
        /// 协程：解析当前行并下发到 View。
        /// 关键流程：
        /// 1) 异步获取说话人与正文的本地化字符串；
        /// 2) 遍历 LearnableWords，命中即把词条名替换为富文本颜色高亮，并标记为已学习；
        /// 3) 通过 staleCheck 校验“是否仍然是当前应展示的行”；
        /// 4) 调用 onTypewriterStarting，再下发说话人/打字机事件。
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

            // --- 2) 关键词高亮 + 词条学习 ---
            string formattedText = contentText;
            IReadOnlyList<WordSO> learnableWords = line.LearnableWords;
            if (learnableWords != null)
            {
                for (int i = 0; i < learnableWords.Count; i++)
                {
                    WordSO word = learnableWords[i];
                    if (word == null || word.DisplayName == null) continue;

                    var wordNameHandle = word.DisplayName.GetLocalizedStringAsync();
                    yield return wordNameHandle;
                    string wordDisplayName = wordNameHandle.Result ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(wordDisplayName)) continue;

                    if (formattedText.IndexOf(wordDisplayName, StringComparison.Ordinal) < 0) continue;

                    string highlighted = "<color=" + _highlightColorHex + ">" + wordDisplayName + "</color>";
                    formattedText = formattedText.Replace(wordDisplayName, highlighted);
                    TryLearnWord(word);
                }
            }

            // --- 3) 防并发串线：异步过程中若已切句/结束对话，丢弃结果 ---
            if (staleCheck != null && !staleCheck()) yield break;

            // --- 4) 下发到 View ---
            EventBus.Raise(new DialogueSpeakerChangedEvent { SpeakerText = speakerText });
            onTypewriterStarting?.Invoke();
            EventBus.Raise(new DialogueTypewriterRequestEvent
            {
                FormattedText = formattedText,
                Speed = _typewriterSpeed
            });
        }

        /// <summary>
        /// 记录词条学习状态并发布事件（仅首次学习会发布）。
        /// </summary>
        private void TryLearnWord(WordSO word)
        {
            if (word == null || string.IsNullOrWhiteSpace(word.ID)) return;

            string normalizedWordId = word.ID.Trim();
            if (!_learnedWordIds.Add(normalizedWordId)) return;

            EventBus.Raise(new DialogueWordLearnedEvent
            {
                WordID = normalizedWordId,
                Word = word
            });
        }
    }
}
