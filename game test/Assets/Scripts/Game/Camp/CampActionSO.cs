using UnityEngine;

namespace IndieGame.Gameplay.Camp
{
    /// <summary>
    /// 露营功能枚举：
    /// 用于标识 Camp 菜单按钮的逻辑类型。
    /// </summary>
    // 注意：该枚举以 int 形式序列化在已有的 CampActionSO 资产中，
    // 新增项一律追加在末尾，不得插入或调整已有项的顺序，否则会破坏已保存资产的 ActionID 引用。
    public enum CampActionID
    {
        // 制作
        Crafting,
        // 背包
        Inventory,
        // 回忆/日志
        Memory,
        // 技能树（当前露营菜单未使用，技能树入口已迁移至 HUD 按钮）
        SkillTree,
        // 睡觉
        Sleep,
        // 装备
        Equip,
        // 训练
        Training,
        // 地图
        Map
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

    }
}
