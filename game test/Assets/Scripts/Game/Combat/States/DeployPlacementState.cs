using IndieGame.Core;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 上场放置状态（Overlay，继承选点瞄准骨架）：
    /// 指向解析/NavMesh 校验/指示器驱动由 AimingOverlayStateBase 提供，
    /// 本类只负责上场业务——确认（再按上场键）时生成战斗体、取消（ESC/手柄B）退出。
    /// </summary>
    public class DeployPlacementState : AimingOverlayStateBase
    {
        /// <summary> 正在放置的名册成员 </summary>
        public RosterMember Member { get; }

        public DeployPlacementState(RosterMember member)
        {
            Member = member;
        }

        protected override float GetAimRadius(CombatManager context)
        {
            return context.Config != null ? context.Config.DeployPlacementRadius : 8f;
        }

        protected override void OnAimingEntered(CombatManager context)
        {
            EventBus.Raise(new DeployPlacementStartedEvent { Member = Member });
        }

        /// <summary>
        /// 确认放置：落点合法则生成战斗体上场并退出放置态；非法则广播拒绝提示。
        /// </summary>
        public override void Confirm(CombatManager context)
        {
            if (!IsValidPoint)
            {
                EventBus.Raise(new DeployRejectedEvent
                {
                    Member = Member,
                    Reason = DeployRejectReason.InvalidPlacement
                });
                return;
            }

            context.DeployMember(Member, SnappedPoint);
            context.PopOverlayState();
            EventBus.Raise(new DeployPlacementEndedEvent { Confirmed = true });
        }

        /// <summary>
        /// 取消放置。
        /// </summary>
        public override void Cancel(CombatManager context)
        {
            context.PopOverlayState();
            EventBus.Raise(new DeployPlacementEndedEvent { Confirmed = false });
        }
    }
}
