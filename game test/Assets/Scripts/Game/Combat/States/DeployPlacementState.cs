using UnityEngine;
using UnityEngine.AI;
using IndieGame.Core;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 上场放置状态（Overlay）：
    /// 放置期间战斗照常进行（不暂停、不减速）。
    /// 每帧从 AimInputRouter 解析指向落点（鼠标移动 / 手柄右摇杆，最后活跃者胜出），
    /// 用 NavMesh.SamplePosition 校验落点合法性并驱动指示器变色。
    /// 确认（再按上场键）/取消（ESC/手柄B）由 CombatManager 统一接收输入后转调本状态。
    /// </summary>
    public class DeployPlacementState : CombatState
    {
        /// <summary> 正在放置的名册成员 </summary>
        public RosterMember Member { get; }

        // 指向输入路由（放置态生命周期内有效）
        private AimInputRouter _router;
        // 放置基准点（进入放置态时冻结为主角战斗体位置，保证范围圈视觉稳定）
        private Vector3 _origin;
        // 当前解析出的落点（吸附到 NavMesh 后）
        private Vector3 _snappedPoint;
        // 当前落点是否合法
        private bool _isValid;

        public DeployPlacementState(RosterMember member)
        {
            Member = member;
        }

        public override void OnEnter(CombatManager context)
        {
            _router = new AimInputRouter(context.InputReader, context.Config);

            // 基准点：主角在场则以主角为圆心，否则用出生点兜底
            CombatUnit protagonist = context.Roster.Protagonist?.FieldUnit;
            _origin = protagonist != null
                ? protagonist.transform.position
                : (context.SceneRefs.PlayerSpawnPoint != null ? context.SceneRefs.PlayerSpawnPoint.position : Vector3.zero);

            _snappedPoint = _origin;
            _isValid = false;

            float radius = context.Config != null ? context.Config.DeployPlacementRadius : 8f;
            if (context.SceneRefs.PlacementIndicator != null)
            {
                context.SceneRefs.PlacementIndicator.Show(_origin, radius);
            }

            EventBus.Raise(new DeployPlacementStartedEvent { Member = Member });
        }

        public override void OnUpdate(CombatManager context)
        {
            if (context.InputReader == null || context.Config == null) return;

            // 喂入两路指向输入（轮询缓存值即可，无需订阅事件）
            _router.NotifyPointer(context.InputReader.CurrentPointerPosition);
            _router.NotifyStick(context.InputReader.CurrentAimStick);

            float radius = context.Config.DeployPlacementRadius;
            if (!_router.TryGetPoint(_origin, radius, out Vector3 rawPoint)) return;

            // 落点合法性：能在容差内吸附到 NavMesh 即可放置
            float sampleDistance = context.Config.PlacementSampleDistance;
            if (NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            {
                _snappedPoint = hit.position;
                _isValid = true;
            }
            else
            {
                _snappedPoint = rawPoint;
                _isValid = false;
            }

            if (context.SceneRefs.PlacementIndicator != null)
            {
                context.SceneRefs.PlacementIndicator.UpdatePoint(_snappedPoint, _isValid);
            }
        }

        public override void OnExit(CombatManager context)
        {
            if (context.SceneRefs.PlacementIndicator != null)
            {
                context.SceneRefs.PlacementIndicator.Hide();
            }
        }

        /// <summary>
        /// 确认放置（玩家在放置态再按上场键）：
        /// 落点合法则生成战斗体上场并退出放置态；非法则广播拒绝提示。
        /// </summary>
        public void ConfirmPlacement(CombatManager context)
        {
            if (!_isValid)
            {
                EventBus.Raise(new DeployRejectedEvent
                {
                    Member = Member,
                    Reason = DeployRejectReason.InvalidPlacement
                });
                return;
            }

            context.DeployMember(Member, _snappedPoint);
            context.PopOverlayState();
            EventBus.Raise(new DeployPlacementEndedEvent { Confirmed = true });
        }

        /// <summary>
        /// 取消放置（ESC/手柄B）。
        /// </summary>
        public void CancelPlacement(CombatManager context)
        {
            context.PopOverlayState();
            EventBus.Raise(new DeployPlacementEndedEvent { Confirmed = false });
        }
    }
}
