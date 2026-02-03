using UnityEngine;

namespace IndieGame.Gameplay.Camp
{
    /// <summary>
    /// 露营功能枚举：
    /// 用于标识 Camp 菜单按钮的逻辑类型。
    /// </summary>
    public enum CampActionID
    {
        // 制作
        Crafting,
        // 背包
        Inventory,
        // 回忆/日志
        Memory,
        // 技能树
        SkillTree,
        // 商铺管理
        ShopManagement,
        // 睡觉
        Sleep
    }

    /// <summary>
    /// 时间消耗类型：
    /// 用于描述不同操作对时间的影响程度。
    /// </summary>
    public enum TimeCostType
    {
        None,
        Small,
        Large
    }

    /// <summary>
    /// 露营动作配置（ScriptableObject）：
    /// 提供 UI 显示信息与逻辑类型。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Camp/Camp Action")]
    public class CampActionSO : ScriptableObject
    {
        [Header("Base Info")]
        // 动作唯一 ID
        public CampActionID ActionID;
        // 显示名称（直接用于 UI）
        public string DisplayName;
        // 描述信息（供 UI 提示）
        [TextArea] public string Description;
        // 图标
        public Sprite Icon;

        [Header("Time Cost")]
        // 时间消耗类型
        public TimeCostType TimeCostType = TimeCostType.None;
    }
}
