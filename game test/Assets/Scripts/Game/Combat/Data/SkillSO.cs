using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 技能配置基类（命令模式，与 ItemSO.Use / BoardEventSO 的项目惯例一致）：
    /// v2 玩法下技能不需要玩家瞄准——按键触发后由技能自身根据施法者的
    /// 位置、面朝方向与当前索敌目标解析作用范围并立即结算。
    /// 子类实现 Execute 完成具体效果（伤害/治疗/位移等）。
    /// </summary>
    public abstract class SkillSO : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("技能唯一 ID（日志与未来存档用）")]
        public string ID;

        [Tooltip("技能图标（HUD 名册槽显示）")]
        public Sprite Icon;

        [Tooltip("技能描述（HUD 提示用）")]
        [TextArea]
        public string Description;

        /// <summary>
        /// 释放技能：
        /// 由 SkillCaster 在充能满且玩家按下技能键（或入场技触发）时调用。
        /// 实现方不应假设目标存在——无目标时需自行退化处理（如以自身为圆心）。
        /// </summary>
        /// <param name="caster">施法单位（提供位置/朝向/属性/当前目标）</param>
        public abstract void Execute(CombatUnit caster);
    }
}
