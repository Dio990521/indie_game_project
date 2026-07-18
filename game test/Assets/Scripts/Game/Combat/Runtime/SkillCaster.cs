using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 技能释放器：
    /// v2 玩法下技能无需瞄准——充能满后玩家按键触发 TryCast，
    /// 技能自身按施法者位置/朝向/当前目标解析范围并立即结算。
    /// 另提供入场技入口（上场瞬间无视充能直接释放）。
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillCaster : MonoBehaviour
    {
        private CombatUnit _self;

        /// <summary> 当前武器是否配置了技能 </summary>
        public bool HasSkill => _self != null && _self.WeaponData != null && _self.WeaponData.Skill != null;

        /// <summary> 当前技能配置（可能为 null） </summary>
        public SkillSO Skill => _self != null && _self.WeaponData != null ? _self.WeaponData.Skill : null;

        /// <summary>
        /// 初始化（由 CombatUnit.Initialize 调用）。
        /// </summary>
        public void Initialize(CombatUnit self)
        {
            _self = self;
        }

        /// <summary>
        /// 尝试释放武器技能：
        /// 校验存活/技能存在/充能满 → 执行技能 → 清空充能 → 广播 SkillReleasedEvent。
        /// 规则性拒绝（不在场/充能未满）应在调用前由 CombatRoster.CanCastSkill 拦截，
        /// 这里只做最终一致性兜底。
        /// </summary>
        /// <returns>true = 成功释放</returns>
        public bool TryCast()
        {
            if (_self == null || !_self.IsAlive || !HasSkill) return false;
            if (_self.Charge == null || !_self.Charge.IsFull) return false;

            SkillSO skill = Skill;
            skill.Execute(_self);
            _self.Charge.ResetCharge();

            EventBus.Raise(new SkillReleasedEvent { Unit = _self, Skill = skill });
            DebugTools.Log($"<color=cyan>[Combat] {_self.name} 释放技能 {skill.name}</color>");
            return true;
        }

        /// <summary>
        /// 释放入场技（上场瞬间触发，不消耗充能、不广播 SkillReleasedEvent）。
        /// </summary>
        public void CastEntrySkill(SkillSO entrySkill)
        {
            if (_self == null || !_self.IsAlive || entrySkill == null) return;
            entrySkill.Execute(_self);
            DebugTools.Log($"<color=cyan>[Combat] {_self.name} 触发入场技 {entrySkill.name}</color>");
        }
    }
}
