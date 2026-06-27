using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using IndieGame.Gameplay.Stats;

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
    /// 4) 同一个词条可以同时承担"对话词汇"与"武器强化前缀"两种用途：
    ///    isWeaponPrefix=true 时，DisplayName 会作为强化后武器名称的前缀文字，
    ///    PrefixModifiers/SpecialEffects 描述强化时叠加的属性与特殊效果。
    /// </summary>
    [CreateAssetMenu(fileName = "Word", menuName = "IndieGame/Dialogue/Word")]
    public class WordSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("词条唯一 ID（建议使用短英文键，避免后续重命名导致存档失效）")]
        [SerializeField] private string id;

        [Header("Localized Content")]
        [Tooltip("词条显示名（用于对话中关键词匹配与高亮，也作为武器强化前缀文字）")]
        [SerializeField] private LocalizedString displayName;

        [Tooltip("词条说明（用于图鉴/知识库面板展示）")]
        [SerializeField] private LocalizedString description;

        [Header("武器强化前缀（可选）")]
        [Tooltip("是否可用作武器强化前缀；为 false 时仅作对话词汇使用")]
        [SerializeField] private bool isWeaponPrefix;

        [Tooltip("强化时叠加的数值加成（攻击+5/防御+5/HP+/充能+ 这类）")]
        [SerializeField] private List<StatModifierData> prefixModifiers = new List<StatModifierData>();

        [Tooltip("强化时叠加的特殊效果（出血/中毒/暴击/攻速/蓄力/状态抗性），仅用于 UI 文字展示，不接入战斗结算")]
        [SerializeField] private List<PrefixSpecialEffect> specialEffects = new List<PrefixSpecialEffect>();

        /// <summary> 词条唯一 ID </summary>
        public string ID => id;
        /// <summary> 本地化显示名 </summary>
        public LocalizedString DisplayName => displayName;
        /// <summary> 本地化描述文本 </summary>
        public LocalizedString Description => description;

        /// <summary> 是否可用作武器强化前缀 </summary>
        public bool IsWeaponPrefix => isWeaponPrefix;
        /// <summary> 强化数值加成（只读暴露） </summary>
        public IReadOnlyList<StatModifierData> PrefixModifiers => prefixModifiers;
        /// <summary> 强化特殊效果（只读暴露） </summary>
        public IReadOnlyList<PrefixSpecialEffect> SpecialEffects => specialEffects;
    }
}
