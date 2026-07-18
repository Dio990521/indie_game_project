using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 范围伤害技能（Phase 1 唯一技能实现）：
    /// 按 Shape 自动解析作用范围——以自身为圆心 / 以当前目标为圆心 / 朝目标方向的直线，
    /// 对范围内的敌方存活单位逐个结算伤害。
    /// 目标筛选走 CombatUnitRegistry（纯 C# 注册表 + 复用缓冲列表），零物理查询、零逐帧分配。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Skill/Area Damage")]
    public class AreaDamageSkillSO : SkillSO
    {
        [Header("作用形态")]
        [Tooltip("范围形态：自身圆心 / 目标圆心 / 朝目标直线")]
        public SkillShape Shape = SkillShape.SelfRadial;

        [Tooltip("圆形范围半径（SelfRadial / TargetCircle 使用）")]
        public float Radius = 3f;

        [Tooltip("直线长度（LineToTarget 使用）")]
        public float LineLength = 8f;

        [Tooltip("直线宽度（LineToTarget 使用）")]
        public float LineWidth = 2f;

        [Header("伤害")]
        [Tooltip("技能基础伤害")]
        public int BaseDamage = 30;

        [Tooltip("攻击力加成系数（附加伤害 = 攻击力 × 系数）")]
        public float AttackScaling = 1f;

        // 目标筛选的复用缓冲：技能结算只在主线程按键触发，静态共享安全
        private static readonly List<CombatUnit> _targetBuffer = new List<CombatUnit>(8);

        public override void Execute(CombatUnit caster)
        {
            if (caster == null) return;
            CombatManager manager = CombatManager.Instance;
            if (manager == null) return;

            // 敌对阵营：玩家技能打敌人，（未来）敌人技能打玩家
            CombatTeam targetTeam = caster.Team == CombatTeam.Player ? CombatTeam.Enemy : CombatTeam.Player;
            manager.Registry.GetAliveUnitsNonAlloc(targetTeam, _targetBuffer);
            if (_targetBuffer.Count == 0) return;

            // 解析范围参数（目标缺失时按注释的退化规则处理）
            Vector3 casterPos = caster.transform.position;
            CombatUnit currentTarget = caster.CurrentTarget;

            int attack = Mathf.RoundToInt(caster.Stats != null ? caster.Stats.Attack.Value : 0f);
            int bonusDamage = Mathf.RoundToInt(attack * AttackScaling);

            switch (Shape)
            {
                case SkillShape.SelfRadial:
                    DamageInCircle(casterPos, Radius, bonusDamage);
                    break;

                case SkillShape.TargetCircle:
                {
                    // 无目标时退化为以自身为圆心
                    Vector3 center = currentTarget != null ? currentTarget.transform.position : casterPos;
                    DamageInCircle(center, Radius, bonusDamage);
                    break;
                }

                case SkillShape.LineToTarget:
                {
                    // 朝当前目标方向；无目标时沿施法者面朝方向
                    Vector3 dir = currentTarget != null
                        ? currentTarget.transform.position - casterPos
                        : caster.transform.forward;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) dir = caster.transform.forward;
                    dir.Normalize();
                    DamageInLine(casterPos, dir, LineLength, LineWidth, bonusDamage);
                    break;
                }
            }
        }

        /// <summary>
        /// 圆形范围结算：平面距离（忽略 Y）平方比较，避免开方。
        /// </summary>
        private void DamageInCircle(Vector3 center, float radius, int bonusDamage)
        {
            float sqrRadius = radius * radius;
            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                CombatUnit unit = _targetBuffer[i];
                Vector3 offset = unit.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude > sqrRadius) continue;
                ApplyDamage(unit, bonusDamage);
            }
        }

        /// <summary>
        /// 直线（矩形）范围结算：把目标位置投影到直线方向，
        /// 纵向落在 [0, length]、横向距离不超过半宽即命中。
        /// </summary>
        private void DamageInLine(Vector3 origin, Vector3 dir, float length, float width, int bonusDamage)
        {
            float halfWidth = width * 0.5f;
            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                CombatUnit unit = _targetBuffer[i];
                Vector3 offset = unit.transform.position - origin;
                offset.y = 0f;

                // 沿直线方向的投影距离（纵向）
                float along = Vector3.Dot(offset, dir);
                if (along < 0f || along > length) continue;

                // 垂直于直线方向的偏移（横向）
                Vector3 lateral = offset - dir * along;
                if (lateral.sqrMagnitude > halfWidth * halfWidth) continue;

                ApplyDamage(unit, bonusDamage);
            }
        }

        /// <summary>
        /// 对单个目标结算技能伤害（走统一伤害公式，考虑目标防御）。
        /// </summary>
        private void ApplyDamage(CombatUnit target, int bonusDamage)
        {
            if (target == null || !target.IsAlive || target.Stats == null) return;
            int defense = Mathf.RoundToInt(target.Stats.Defense.Value);
            int damage = CombatFormulas.CalculateDamage(BaseDamage + bonusDamage, 0, defense);
            target.Stats.TakeDamage(damage);
        }
    }
}
