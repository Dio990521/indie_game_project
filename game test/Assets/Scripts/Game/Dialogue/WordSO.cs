using UnityEngine;
using UnityEngine.Localization;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 词条配置（ScriptableObject）：
    /// 用于描述一个“可被学习的知识点/关键词”。
    ///
    /// 设计说明：
    /// 1) ID 作为运行时与存档层面的稳定键，必须全局唯一且长期稳定。
    /// 2) DisplayName / Description 使用 LocalizedString，保证多语言下可正确显示。
    /// 3) 本 SO 只存静态数据，不存任何运行时状态（如是否已学习）。
    /// </summary>
    [CreateAssetMenu(fileName = "Word", menuName = "IndieGame/Dialogue/Word")]
    public class WordSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("词条唯一 ID（建议使用短英文键，避免后续重命名导致存档失效）")]
        [SerializeField] private string id;

        [Header("Localized Content")]
        [Tooltip("词条显示名（用于对话中关键词匹配与高亮）")]
        [SerializeField] private LocalizedString displayName;

        [Tooltip("词条说明（用于图鉴/知识库面板展示）")]
        [SerializeField] private LocalizedString description;

        /// <summary> 词条唯一 ID </summary>
        public string ID => id;
        /// <summary> 本地化显示名 </summary>
        public LocalizedString DisplayName => displayName;
        /// <summary> 本地化描述文本 </summary>
        public LocalizedString Description => description;
    }
}
