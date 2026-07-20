using UnityEngine;
using UnityEngine.AI;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 选点瞄准 Overlay 状态基类（上场放置 / 道具瞄准共用骨架）：
    /// - 每帧从 AimInputRouter 解析指向落点（鼠标移动 / 手柄右摇杆，最后活跃者胜出）；
    /// - NavMesh.SamplePosition 校验落点合法性并驱动指示器变色；
    /// - 基准点冻结为进入瞄准时的主角战斗体位置（保证范围圈视觉稳定）。
    /// 子类实现 GetAimRadius（半径来源）与 Confirm/Cancel（确认与取消的业务动作），
    /// 确认/取消的按键路由由 CombatManager 统一接收后转调。
    /// 瞄准期间战斗照常进行（不暂停、不减速）。
    /// </summary>
    public abstract class AimingOverlayStateBase : CombatState
    {
        // 指向输入路由（瞄准态生命周期内有效）
        private AimInputRouter _router;

        /// <summary> 瞄准基准点（进入时冻结的主角位置） </summary>
        protected Vector3 Origin { get; private set; }

        /// <summary> 当前解析出的落点（吸附到 NavMesh 后） </summary>
        protected Vector3 SnappedPoint { get; private set; }

        /// <summary> 当前落点是否合法（可放置/可施放） </summary>
        protected bool IsValidPoint { get; private set; }

        /// <summary>
        /// 本次瞄准的最大半径（落点被 Clamp 到以基准点为圆心的该半径圆内）。
        /// </summary>
        protected abstract float GetAimRadius(CombatManager context);

        /// <summary>
        /// 瞄准进入后的业务钩子（广播各自的开始事件等）。
        /// </summary>
        protected virtual void OnAimingEntered(CombatManager context) { }

        /// <summary>
        /// 确认（玩家再按触发键）：由 CombatManager 转调。
        /// </summary>
        public abstract void Confirm(CombatManager context);

        /// <summary>
        /// 取消（ESC/手柄B）：由 CombatManager 转调。
        /// </summary>
        public abstract void Cancel(CombatManager context);

        public override void OnEnter(CombatManager context)
        {
            _router = new AimInputRouter(context.InputReader, context.Config);

            // 基准点：主角在场则以主角为圆心，否则用出生点兜底
            CombatUnit protagonist = context.Roster.Protagonist?.FieldUnit;
            Origin = protagonist != null
                ? protagonist.transform.position
                : (context.SceneRefs.PlayerSpawnPoint != null ? context.SceneRefs.PlayerSpawnPoint.position : Vector3.zero);

            SnappedPoint = Origin;
            IsValidPoint = false;

            if (context.SceneRefs.PlacementIndicator != null)
            {
                context.SceneRefs.PlacementIndicator.Show(Origin, GetAimRadius(context));
            }

            OnAimingEntered(context);
        }

        public override void OnUpdate(CombatManager context)
        {
            if (context.InputReader == null || context.Config == null) return;

            // 喂入两路指向输入（轮询缓存值即可，无需订阅事件）
            _router.NotifyPointer(context.InputReader.CurrentPointerPosition);
            _router.NotifyStick(context.InputReader.CurrentAimStick);

            if (!_router.TryGetPoint(Origin, GetAimRadius(context), out Vector3 rawPoint)) return;

            // 落点合法性：能在容差内吸附到 NavMesh 即为合法
            float sampleDistance = context.Config.PlacementSampleDistance;
            if (NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
            {
                SnappedPoint = hit.position;
                IsValidPoint = true;
            }
            else
            {
                SnappedPoint = rawPoint;
                IsValidPoint = false;
            }

            if (context.SceneRefs.PlacementIndicator != null)
            {
                context.SceneRefs.PlacementIndicator.UpdatePoint(SnappedPoint, IsValidPoint);
            }
        }

        public override void OnExit(CombatManager context)
        {
            if (context.SceneRefs.PlacementIndicator != null)
            {
                context.SceneRefs.PlacementIndicator.Hide();
            }
        }
    }
}
