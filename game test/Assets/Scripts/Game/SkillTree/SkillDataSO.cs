using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.SkillTree
{
    /// <summary>
    /// 技能效果类型：
    /// 扩展时在此枚举追加值，SkillTreeSystem.ApplySkillEffect 中增加对应 case 分支。
    /// </summary>
    public enum SkillEffectType
    {
        // 无效果（仅解锁状态）
        None = 0,
        // 提升行动点上限（effectValue 代表增加的整数值）
        IncreaseMaxActionPoint = 1,
    }

    /// <summary>
    /// 技能树分类：
    /// int 值与 SkillTreeController 中的 SkillTreeTab 枚举顺序保持一致，用于 Tab 索引映射。
    /// </summary>
    public enum SkillTreeCategory
    {
        Combat      = 0,
        Exploration = 1,
        Crafting    = 2,
    }

    /// <summary>
    /// 技能静态配置（ScriptableObject）：
    /// 定义单个技能的全部策划数据，运行时由 SkillTreeSystem 读取引用。
    /// 不保存任何运行时状态（已学/未学由 SkillTreeSystem 持有）。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/SkillTree/SkillData", fileName = "SkillData_")]
    public class SkillDataSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("技能唯一 ID，全局唯一，用于存档与前置关系索引。")]
        [SerializeField] private string skillId;

        [Tooltip("技能显示名称。")]
        [SerializeField] private string skillName = "Unnamed Skill";

        [Tooltip("技能描述。")]
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Tooltip("技能图标。")]
        [SerializeField] private Sprite icon;

        [Header("Classification")]
        [Tooltip("所属技能树分类，决定在哪个 Tab 下显示。")]
        [SerializeField] private SkillTreeCategory category = SkillTreeCategory.Exploration;

        [Header("Cost")]
        [Tooltip("学习该技能所需的技能点（SP）。")]
        [SerializeField] private int spCost = 1;

        [Header("Prerequisites")]
        [Tooltip("学习本技能前必须已学习的技能 ID 列表，空列表表示无前置。")]
        [SerializeField] private List<string> prerequisiteSkillIds = new List<string>();

        [Header("Effect")]
        [Tooltip("学习后产生的效果类型。")]
        [SerializeField] private SkillEffectType effectType = SkillEffectType.None;

        [Tooltip("效果数值，含义由 effectType 决定（如 IncreaseMaxActionPoint 时为增加的整数值）。")]
        [SerializeField] private float effectValue;

        [Header("UI Layout")]
        [Tooltip("在技能网格中的坐标（x=列, y=行），从 (0,0) 起。用于动态排布节点位置。")]
        [SerializeField] private Vector2Int gridPosition;

        // --- 只读属性 ---
        public string SkillId           => skillId;
        public string SkillName         => string.IsNullOrWhiteSpace(skillName) ? "Unnamed Skill" : skillName;
        public string Description       => description;
        public Sprite Icon              => icon;
        public SkillTreeCategory Category => category;
        public int SpCost               => Mathf.Max(0, spCost);
        public IReadOnlyList<string> PrerequisiteSkillIds => prerequisiteSkillIds;
        public SkillEffectType EffectType => effectType;
        public float EffectValue        => effectValue;
        public Vector2Int GridPosition  => gridPosition;
    }
}
