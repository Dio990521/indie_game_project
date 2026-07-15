using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.FogOfWar;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// BoardMovementController 的格子效果 partial：
    /// <para>包含两类内容：</para>
    /// <para>
    /// 1) <b>格子事件订阅处理器</b>：OnXxxRequested 7 个方法把 EventBus 发来的格子请求写入 <c>_fx</c>，
    ///    供主流程的 HandleSegmentCompleted 协程在合适时机消费；
    /// </para>
    /// <para>
    /// 2) <b>特效协程</b>：人体大炮弹射、传送格瞬移等独立特效协程。它们涉及实体动画、地图查询、
    ///    雾战揭示等复杂联动，与基础"步进 → 落地"流程相对独立。
    /// </para>
    /// <para>
    /// 拆分到本文件后，BoardMovementController.cs 主文件可专注于"步进决策与生命周期管理"。
    /// </para>
    /// </summary>
    public partial class BoardMovementController
    {
        // ===================== 格子效果协程 =====================

        /// <summary>
        /// [人体大炮] 弹射协程：连续弹射 + 落点二次触发的格子效果（传送/方向格）。
        /// 该协程负责自身的流程终止（FinishMove / AdvanceToNextStep），
        /// 调用方在调用后应直接 yield break。
        /// </summary>
        private IEnumerator ProcessCannonChainCoroutine()
        {
            // 使用 while 循环支持连续弹射（落点也是大炮格时继续触发）
            while (_fx.CannonLaunch)
            {
                _fx.CannonLaunch = false;
                ComboMoveSystem.IncrementCombo(); // 大炮每次弹射计一次连锁
                yield return DoCannonLaunch();
            }

            // 大炮落点可能触发了其他格子效果，依次检查并处理
            if (_fx.Teleport && !_isTeleporting)
            {
                _fx.Teleport   = false;
                _isTeleporting = true;
                ComboMoveSystem.IncrementCombo(); // 大炮落点为传送格，再次连锁
                yield return DoTeleport();
                _isTeleporting = false;
                FinishMove();
                yield break;
            }

            if (_fx.DirectionalSteps > 0)
            {
                ComboMoveSystem.IncrementCombo(); // 大炮落点为方向格，再次连锁
                _stepsRemaining       = _fx.DirectionalSteps;
                _fx.ForcedNextNodeId  = _fx.DirectionalNodeId;
                _fx.DirectionalSteps  = 0;
                _fx.DirectionalNodeId = -1;
                AdvanceToNextStep();
                yield break;
            }

            // [扭曲格] 大炮落点为扭曲格时触发强制滑行，补充1步并交由 AdvanceToNextStep 消费方向锁
            if (_fx.ForcedNextNodeId >= 0)
            {
                ComboMoveSystem.IncrementCombo();
                _stepsRemaining = 1;
                AdvanceToNextStep();
                yield break;
            }

            FinishMove();
        }

        /// <summary>
        /// [传送格] 执行单次传送：标记 _fx.Teleport 已消费，做一次 ComboMove 计数，并执行传送动画。
        /// 调用方负责后续的 FinishMove。
        /// </summary>
        private IEnumerator ExecuteTeleportRoutine()
        {
            _fx.Teleport   = false;
            _isTeleporting = true;
            ComboMoveSystem.IncrementCombo(); // 传送格触发传送，进入连锁
            yield return DoTeleport();
            _isTeleporting = false;
        }

        /// <summary>
        /// 执行炮弹弹射：随机选目标节点 → 在起飞前预选落地朝向 → 抛物线飞行（空中转体）→ 落地减速定向 → 触发目标格子效果。
        /// </summary>
        private IEnumerator DoCannonLaunch()
        {
            List<MapWaypoint> allNodes = BoardMapManager.Instance != null
                ? BoardMapManager.Instance.GetAllNodes()
                : new List<MapWaypoint>();

            MapWaypoint current = _activeEntity.CurrentNode;
            allNodes.Remove(current);

            if (allNodes.Count == 0) yield break;

            MapWaypoint target = allNodes[UnityEngine.Random.Range(0, allNodes.Count)];
            DebugTools.Log($"<color=orange>[Cannon Tile]</color> 弹射目标：{target.nodeID} ({target.name})");

            // 在起飞前就决定落地朝向，这样旋转动画的终点在空中就已确定。
            // 从目标节点所有出口中随机选一个作为落地后首步方向。
            Quaternion? landingFacing = null;
            List<MapWaypoint> exits = target.GetValidNextNodes(null);
            if (exits.Count > 0)
            {
                MapWaypoint chosenExit = exits[UnityEngine.Random.Range(0, exits.Count)];
                // 存储预选方向，下次BeginMove时注入首步强制节点，避免弹出岔路UI
                _cannonPresetFirstStepNodeId = chosenExit.nodeID;
                // 计算朝向：从落点指向选定出口，忽略Y轴高度差
                Vector3 dir = chosenExit.transform.position - target.transform.position;
                dir.y = 0f;
                if (dir != Vector3.zero)
                    landingFacing = Quaternion.LookRotation(dir.normalized);
                DebugTools.Log($"<color=orange>[Cannon Tile]</color> 预选落地朝向：出口节点 {chosenExit.nodeID}");
            }

            // 记录弹射起点，落地后一次性揭开整段 XZ 轨迹（单次 GPU 上传，性能最优）
            Vector3 launchStartPos = _activeEntity.transform.position;

            // 执行抛物线飞行：传入自转参数和落地目标朝向
            yield return _activeEntity.LaunchParabolic(
                target,
                _fx.CannonArcHeight,
                _fx.CannonLaunchSpeed,
                originNode: null,
                spinSpeed: _fx.CannonSpinSpeed,
                landingFacing: landingFacing,
                settleExtraRotations: _fx.CannonSettleExtraRotations
            );

            FogOfWarManager.Instance?.RevealLine(launchStartPos, target.transform.position);

            // 触发落点格子效果（作为最终落点处理）
            yield return HandleNodeArrival(target, true);
        }

        /// <summary>
        /// 执行传送：按指定节点 ID 瞬间移动玩家 → 触发目标格子效果。
        /// </summary>
        private IEnumerator DoTeleport()
        {
            MapWaypoint target = BoardMapManager.Instance != null
                ? BoardMapManager.Instance.GetNode(_fx.TeleportTargetId)
                : null;

            if (target == null)
            {
                DebugTools.LogWarning($"[Teleport Tile] 找不到节点 ID={_fx.TeleportTargetId}，跳过传送。");
                yield break;
            }

            DebugTools.Log($"<color=cyan>[Teleport Tile]</color> 传送目标：{target.nodeID} ({target.name})");
            // 瞬间传送：snap 坐标 + 重置来路（防止传送后被当成原路返回）
            _activeEntity.SetCurrentNode(target, true, true);

            yield return HandleNodeArrival(target, true);
        }

        // ===================== 格子事件订阅处理器 =====================

        /// <summary>
        /// 接收人体大炮弹射请求事件（仅在移动期间订阅）。
        /// </summary>
        private void OnCannonLaunchRequested(BoardCannonLaunchRequestedEvent evt)
        {
            _fx.CannonLaunch               = true;
            _fx.CannonArcHeight            = evt.ArcHeight;
            _fx.CannonLaunchSpeed          = evt.LaunchSpeed;
            _fx.CannonSpinSpeed            = evt.SpinSpeed;
            _fx.CannonSettleExtraRotations = evt.SettleExtraRotations;
        }

        /// <summary>
        /// 接收方向格移动请求事件（仅在移动期间订阅）。
        /// </summary>
        private void OnDirectionalMoveRequested(BoardDirectionalMoveRequestedEvent evt)
        {
            _fx.DirectionalNodeId = evt.DirectionNodeId;
            _fx.DirectionalSteps  = evt.Steps;
        }

        /// <summary>
        /// 接收传送格传送请求事件（仅在移动期间订阅）。
        /// </summary>
        private void OnTeleportRequested(BoardTeleportRequestedEvent evt)
        {
            _fx.Teleport         = true;
            _fx.TeleportTargetId = evt.TargetNodeId;
        }

        /// <summary>
        /// 接收格子请求的额外步数事件（仅在移动期间订阅）。
        /// </summary>
        private void OnExtraMoveRequested(BoardExtraMoveRequestedEvent evt)
        {
            _fx.ExtraSteps = evt.Steps;
        }

        /// <summary>
        /// 接收扭曲格的强制滑行请求（仅在移动期间订阅）。
        /// </summary>
        private void OnWarpSlideRequested(BoardWarpSlideRequestedEvent evt)
        {
            _fx.ForcedNextNodeId = evt.ForcedNodeId;
        }

        /// <summary>
        /// 接收扭曲格的路径过滤请求（仅在移动期间订阅）。
        /// </summary>
        private void OnWarpFilterPathRequested(BoardWarpFilterPathEvent evt)
        {
            _fx.ProtectedNodeId = evt.ProtectedNodeId;
        }

        // ===================== 外部抵达管线与事件订阅管理 =====================

        /// <summary>
        /// 处理外部跳跃（如飞翼宝具）落点的完整格子效果管线。
        /// 订阅所有格子效果事件，复用 HandleSegmentCompleted 管线，
        /// 支持传送格、大炮格、方向格、扭曲格等所有连锁效果。
        /// 调用方 yield return 本协程，协程结束后实体已处于最终落点，
        /// IsMoving 已重置。
        /// </summary>
        public IEnumerator HandleExternalArrival(MapWaypoint landingNode)
        {
            if (landingNode == null || _playerEntity == null) yield break;
            EnsureInteractionHandler();

            // 初始化为"正在移动、最后一步落地"的状态，完全复用现有管线
            _activeEntity          = _playerEntity;
            _triggerNodeEvents     = true;
            _fx                    = TileEffectPendingState.Default;
            _isMoving              = true;
            _stepsRemaining        = 1; // 模拟落地即终点
            _isTeleporting         = false;
            _allowFirstStepUTurn   = false;

            // 订阅全部格子效果事件（传送、大炮、额外步数、方向格、扭曲格）
            SubscribeSegmentEvent();

            // 监听移动全部结束事件，确保连锁效果（如传送后再触发格子）全部完成
            bool done = false;
            System.Action<BoardMovementEndedEvent> onDone = _ => done = true;
            EventBus.Subscribe(onDone);

            // 启动完整的抵达处理协程（HandleSegmentCompleted 内部最终都会调 FinishMove）
            _arrivalRoutine = StartCoroutine(HandleSegmentCompleted(landingNode));

            // 等待 FinishMove → BoardMovementEndedEvent → done = true
            while (!done) yield return null;

            EventBus.Unsubscribe(onDone);
            // FinishMove 已在内部调用 UnsubscribeSegmentEvent，状态已清理完毕
        }

        /// <summary>
        /// 订阅全部格子效果事件（仅在移动期间保持订阅，FinishMove/ResetToStart 时退订）。
        /// </summary>
        private void SubscribeSegmentEvent()
        {
            EventBus.Subscribe<BoardEntitySegmentCompletedEvent>(OnEntitySegmentCompleted);
            EventBus.Subscribe<BoardExtraMoveRequestedEvent>(OnExtraMoveRequested);
            EventBus.Subscribe<BoardWarpSlideRequestedEvent>(OnWarpSlideRequested);
            EventBus.Subscribe<BoardWarpFilterPathEvent>(OnWarpFilterPathRequested);
            EventBus.Subscribe<BoardCannonLaunchRequestedEvent>(OnCannonLaunchRequested);
            EventBus.Subscribe<BoardTeleportRequestedEvent>(OnTeleportRequested);
            EventBus.Subscribe<BoardDirectionalMoveRequestedEvent>(OnDirectionalMoveRequested);
        }

        /// <summary>
        /// 退订全部格子效果事件（与 SubscribeSegmentEvent 严格成对）。
        /// </summary>
        private void UnsubscribeSegmentEvent()
        {
            EventBus.Unsubscribe<BoardEntitySegmentCompletedEvent>(OnEntitySegmentCompleted);
            EventBus.Unsubscribe<BoardExtraMoveRequestedEvent>(OnExtraMoveRequested);
            EventBus.Unsubscribe<BoardWarpSlideRequestedEvent>(OnWarpSlideRequested);
            EventBus.Unsubscribe<BoardWarpFilterPathEvent>(OnWarpFilterPathRequested);
            EventBus.Unsubscribe<BoardCannonLaunchRequestedEvent>(OnCannonLaunchRequested);
            EventBus.Unsubscribe<BoardTeleportRequestedEvent>(OnTeleportRequested);
            EventBus.Unsubscribe<BoardDirectionalMoveRequestedEvent>(OnDirectionalMoveRequested);
        }
    }
}
