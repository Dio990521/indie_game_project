using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 单句对话数据：
    /// 用于描述“谁说了什么”，以及该句中可学习的词条集合。
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("说话人名称（本地化）")]
        [SerializeField] private LocalizedString speaker;

        [Tooltip("对话正文（本地化）")]
        [SerializeField] private LocalizedString content;

        /// <summary> 说话人 </summary>
        public LocalizedString Speaker => speaker;
        /// <summary> 正文 </summary>
        public LocalizedString Content => content;
    }

    /// <summary>
    /// 对话流配置（ScriptableObject）：
    /// 表示一段完整的线性对话，由多个 DialogueLine 顺序组成。
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueData", menuName = "IndieGame/Dialogue/Dialogue Data")]
    public class DialogueDataSO : ScriptableObject
    {
        [Tooltip("对话行列表，按顺序播放")]
        [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();

        /// <summary> 对话行只读列表 </summary>
        public IReadOnlyList<DialogueLine> Lines => lines;
    }
}
