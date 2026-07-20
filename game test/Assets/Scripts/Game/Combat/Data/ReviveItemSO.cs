using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 复活道具（即时生效，不需瞄准）：
    /// 复活最早阵亡的非主角名册成员——状态回到后台（Backline）、满血、
    /// 按角色的重上场冷却重新计时，之后可正常再上场。
    /// 无阵亡成员时 CanUse 返回 false，不消耗道具。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Item/Revive")]
    public class ReviveItemSO : CombatItemSO
    {
        private void Reset()
        {
            // 复活道具默认即时生效
            RequiresAiming = false;
        }

        public override bool CanUse(CombatManager manager)
        {
            return manager != null && FindDeadMember(manager) != null;
        }

        public override void Execute(CombatManager manager, Vector3 point)
        {
            if (manager == null) return;

            RosterMember member = FindDeadMember(manager);
            if (member == null) return;

            manager.Roster.MarkRevived(member);
            IndieGame.Core.EventBus.Raise(new IndieGame.Core.MemberRevivedEvent { Member = member });
            DebugTools.Log($"<color=cyan>[Combat] 复活道具生效：{member.Definition.name} 回到后台，冷却后可再上场。</color>");
        }

        /// <summary>
        /// 找第一个阵亡的非主角成员（名册顺序即阵亡优先级，Phase 2 简化处理）。
        /// </summary>
        private static RosterMember FindDeadMember(CombatManager manager)
        {
            var members = manager.Roster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].State == RosterMemberState.Dead && !members[i].IsProtagonist)
                {
                    return members[i];
                }
            }
            return null;
        }
    }
}
