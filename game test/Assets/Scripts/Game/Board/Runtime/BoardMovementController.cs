using IndieGame.Core.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘位移控制器：负责驱动棋盘实体（玩家或NPC）在地图上的步进式移动。
    /// 它是逻辑中枢，连接了地图数据（MapWaypoint）、实体表现（BoardEntity）和交互逻辑（BoardInteractionHandler）。
    /// </summary>
    public class BoardMovementController : MonoBehaviour
    {
        [Header("外部依赖")]
        [Tooltip("分叉路口选择器，当移动到有多个出口的节点时调用")]
        public BoardForkSelector forkSelector;

        [Header("游戏对象引用")]
        [Tooltip("玩家在棋盘上的视觉对象/Token的变换组件")]
        public Transform playerToken;

        // --- 公开属性 ---
        /// <summary> 当前实体是否正在移动中 </summary>
        public bool IsMoving => _isMoving;

        /// <summary> 获取玩家实体当前所在节点的 ID，如果实体不存在则返回 -1 </summary>
        public int CurrentNodeId => _playerEntity != null && _playerEntity.CurrentNode != null
            ? _playerEntity.CurrentNode.nodeID
            : -1;

        /// <summary> 暴露玩家实体的引用供外部状态机使用 </summary>
        public BoardEntity PlayerEntity => _playerEntity;

        // --- 内部状态变量 ---
        private BoardEntity _playerEntity;      // 缓存玩家的实体组件
        private BoardEntity _activeEntity;      // 当前正在受控制器驱动移动的实体（可能是玩家，也可能是 NPC）
        private MapWaypoint _startNode;         // 缓存 ID 为 0 的起始节点
        private bool _isMoving = false;         // 移动状态锁
        private bool _triggerNodeEvents = true; // 本次移动是否允许触发地块效果（如 NPC 路过可能不触发）
        private BoardInteractionHandler _interactionHandler; // 专门处理抵达后的逻辑
        private int _stepsRemaining;            // 剩余步数（骰子点数消耗计数）
        private Coroutine _arrivalRoutine;      // 当前正在执行的抵达处理协程
        // [掉头控制] 仅在 BeginMove 时检测到实体处于死胡同时置 true，消耗后立即清除，确保新回合首步允许掉头但途中不允许
        private bool _allowFirstStepUTurn;
        // [额外步数] 前进格/后退格通过 EventBus 写入，HandleSegmentCompleted 末尾统一消费；正数前进，负数后退
        private int _pendingExtraSteps = 0;
        // [扭曲格] 下一步强制走向的目标节点 ID；-1 表示无强制方向
        // 由 WarpTile 通过 EventBus 写入，HandleSegmentCompleted 消费（追加步数），AdvanceToNextStep 最终使用
        private int _pendingForcedNextNodeId = -1;
        // [扭曲格] 下次分叉选择时需要过滤掉的被保护节点 ID；-1 表示无过滤
        private int _pendingProtectedNodeId = -1;
        // [人体大炮] 是否有待执行的弹射请求及参数
        private bool _pendingCannonLaunch = false;
        private float _pendingCannonArcHeight = 5f;
        private float _pendingCannonLaunchSpeed = 12f;
        // [传送格] 是否有待执行的传送请求及目标节点；_isTeleporting 防止连锁触发
        private bool _pendingTeleport         = false;
        private int  _pendingTeleportTargetId = -1;
        private bool _isTeleporting           = false;
        // [方向格] 首步强制走向的节点 ID 及移动步数；-1/0 表示无待执行请求
        private int _pendingDirectionalNodeId = -1;
        private int _pendingDirectionalSteps  = 0;

        private void OnDisable()
        {
            // [安全性清理] 当脚本被禁用或对象销毁时，强制停止所有移动逻辑。
            // 避免因切换场景或关闭棋盘导致协程孤儿运行引发空引用报错。
            StopAllCoroutines();
            UnsubscribeSegmentEvent();
            _isMoving = false;
            if (_activeEntity != null)
            {
                _activeEntity.SetMoveAnimationSpeed(0f);
                _activeEntity.SetMovingState(false);
            }
        }

        /// <summary>
        /// 启动移动（重载：默认驱动玩家实体）。
        /// </summary>
        public void BeginMove(int totalSteps)
        {
            BeginMove(_playerEntity, totalSteps, true);
        }

        /// <summary>
        /// 启动移动的核心方法。
        /// </summary>
        /// <param name="entity">执行移动的实体对象</param>
        /// <param name="totalSteps">移动的总步数（步长）</param>
        /// <param name="triggerNodeEvents">是否触发路径上的节点效果</param>
        public void BeginMove(BoardEntity entity, int totalSteps, bool triggerNodeEvents = true)
        {
            if (_isMoving) return; // 如果正在移动中，禁止重叠触发

            if (entity == null)
            {
                // 容错处理：如果传入实体为空，尝试重新解析并使用玩家实体作为回退
                ResolveReferences(-1);
                entity = _playerEntity;
                if (entity == null) return;
            }

            // 初始化移动参数
            _activeEntity = entity;
            _triggerNodeEvents = triggerNodeEvents;
            _stepsRemaining = totalSteps;
            _pendingExtraSteps = 0;
            _pendingForcedNextNodeId = -1;
            _pendingProtectedNodeId = -1;

            // 同步底层属性：实体在移动时是否检测路径上的事件（如连线中间的交互）
            _activeEntity.TriggerConnectionEvents = triggerNodeEvents;
            _activeEntity.SetMovingState(true);
            _activeEntity.SetMoveAnimationSpeed(1f);

            _isMoving = true;

            // 检测当前实体是否停在死胡同。若是，允许本次移动的首步执行掉头（掉头机制）
            _allowFirstStepUTurn = IsAtDeadEnd(entity);

            // 订阅”路段完成”事件，用于在两点之间移动完后执行决策
            SubscribeSegmentEvent();
            // 开始推进第一步
            AdvanceToNextStep();
        }

        /// <summary>
        /// 强制重置控制器：停止一切移动并将玩家放回起点。
        /// </summary>
        public void ResetToStart()
        {
            StopAllCoroutines();
            _isMoving = false;
            if (forkSelector != null) forkSelector.ClearSelection();

            if (_startNode != null && _playerEntity != null)
            {
                _playerEntity.SetCurrentNode(_startNode, true);
            }
        }

        /// <summary>
        /// 根据节点 ID 强制设置玩家的位置（通常用于存档恢复或传送）。
        /// </summary>
        public void SetCurrentNodeById(int nodeId)
        {
            if (_playerEntity == null) CachePlayerEntity();
            if (_playerEntity == null) return;

            MapWaypoint node = BoardMapManager.Instance != null ? BoardMapManager.Instance.GetNode(nodeId) : null;
            if (node == null) return;

            // 从 Camp 返回时需要保留 LastWaypoint，避免被误判为分叉
            _playerEntity.SetCurrentNode(node, true, false);
        }

        /// <summary>
        /// 解析所有依赖引用：在游戏开始或场景初始化时调用。
        /// </summary>
        /// <param name="preferredNodeId">若大于等于0，则将玩家初始化到该 ID 节点</param>
        public void ResolveReferences(int preferredNodeId)
        {
            EnsureInteractionHandler();

            // 确保地图管理器已建立缓存
            if (BoardMapManager.Instance != null && !BoardMapManager.Instance.IsReady)
            {
                BoardMapManager.Instance.Init();
            }

            // 获取地图起点
            _startNode = BoardMapManager.Instance != null ? BoardMapManager.Instance.GetNode(0) : null;

            // 自动从全局 GameManager 获取玩家引用
            playerToken = GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null
                ? GameManager.Instance.CurrentPlayer.transform
                : null;

            CachePlayerEntity();

            if (preferredNodeId >= 0)
            {
                SetCurrentNodeById(preferredNodeId);
                return;
            }

            // 首次初始化逻辑：如果实体没有位置且有起点，则默认放置在起点
            if (_playerEntity != null && _playerEntity.CurrentNode == null && _startNode != null)
            {
                _playerEntity.SetCurrentNode(_startNode, true);
            }
        }

        /// <summary>
        /// 内部缓存玩家实体的 BoardEntity 组件。
        /// 如果物体上没挂载，则尝试自动添加并设置为玩家属性。
        /// </summary>
        private void CachePlayerEntity()
        {
            _playerEntity = null;
            if (playerToken == null) return;
            _playerEntity = playerToken.GetComponent<BoardEntity>();
            if (_playerEntity == null)
            {
                _playerEntity = playerToken.gameObject.AddComponent<BoardEntity>();
                _playerEntity.SetAsPlayer(true);
            }
        }

        /// <summary>
        /// 核心推进逻辑：决定下一步去哪，或者是结束移动。
        /// </summary>
        private void AdvanceToNextStep()
        {
            // 1. 移动终止检查
            if (!_isMoving || _activeEntity == null || _stepsRemaining <= 0)
            {
                FinishMove();
                return;
            }

            MapWaypoint current = _activeEntity.CurrentNode;
            if (current == null)
            {
                FinishMove();
                return;
            }

            // 2. 获取当前节点的所有有效出口（排除刚才进来的路，防止来回抽搐）
            List<MapWaypoint> validNodes = current.GetValidNextNodes(_activeEntity.LastWaypoint);

            // [扭曲格] 路过时：从候选出口中移除被保护的路径
            if (_pendingProtectedNodeId >= 0)
            {
                MapWaypoint protectedNode = BoardMapManager.Instance != null
                    ? BoardMapManager.Instance.GetNode(_pendingProtectedNodeId)
                    : null;
                if (protectedNode != null) validNodes.Remove(protectedNode);
                _pendingProtectedNodeId = -1;
            }

            // 如果死路一条，则停止
            if (validNodes.Count == 0)
            {
                FinishMove();
                return;
            }

            // [即刻停止 / 掉头机制] 当唯一出口是原路返回（死胡同回退节点）时进行判断
            // · 若是新回合在死胡同的首步（_allowFirstStepUTurn == true）：允许掉头，消耗许可后继续
            // · 若是移动途中抵达死胡同：就地停止，丢弃剩余步数
            bool isUTurnOnly = validNodes.Count == 1
                && _activeEntity.LastWaypoint != null
                && validNodes[0] == _activeEntity.LastWaypoint;
            if (isUTurnOnly)
            {
                if (_allowFirstStepUTurn)
                {
                    _allowFirstStepUTurn = false; // 许可一次性消耗
                }
                else
                {
                    FinishMove(); // 即刻停止，丢弃剩余步数
                    return;
                }
            }

            // 3. 决策逻辑：
            // [扭曲格] 停下时：若有强制方向锁，跳过分叉UI直接走向指定节点
            if (_pendingForcedNextNodeId >= 0)
            {
                int forcedId = _pendingForcedNextNodeId;
                _pendingForcedNextNodeId = -1;
                _pendingProtectedNodeId = -1; // 强制方向时无需过滤，同步清除

                MapWaypoint forcedTarget = BoardMapManager.Instance != null
                    ? BoardMapManager.Instance.GetNode(forcedId)
                    : null;
                WaypointConnection forcedConn = forcedTarget != null
                    ? current.GetConnectionTo(forcedTarget)
                    : null;

                if (forcedConn != null)
                {
                    StartSegment(forcedConn);
                    return;
                }

                // 目标节点不是当前路口的直接出口：打警告，回退到正常分叉逻辑
                DebugTools.LogWarning($"[BoardMovementController] 扭曲格目标节点 ID={forcedId} 不是当前路口的直接出口，忽略方向锁。");
            }

            // A. 如果只有一个出口：直接自动开始该路段的位移
            if (validNodes.Count == 1)
            {
                WaypointConnection conn = current.GetConnectionTo(validNodes[0]);
                StartSegment(conn);
                return;
            }

            // B. 如果有多个出口（岔路）：
            List<WaypointConnection> options = current.GetConnectionsTo(validNodes);

            // 检查是否有外部逻辑通过 EventBus 订阅了岔路决策请求
            if (EventBus.HasSubscribers<BoardForkSelectionRequestedEvent>())
            {
                // 暂停行走动画，等待玩家/AI 进行选择
                _activeEntity.SetMoveAnimationSpeed(0f);
                EventBus.Raise(new BoardForkSelectionRequestedEvent
                {
                    Node = current,
                    Options = options,
                    OnSelected = result => StartSegment(result)
                });
                return;
            }

            // C. 兜底逻辑：如果没有订阅者（通常是 NPC），则执行默认的随机选择
            StartSegment(ChooseNextConnection(current, validNodes));
        }

        /// <summary>
        /// 开始执行一段具体的路径位移（两个地块之间）。
        /// </summary>
        private void StartSegment(WaypointConnection connection)
        {
            if (!_isMoving || _activeEntity == null || connection == null)
            {
                FinishMove();
                return;
            }

            // 恢复动画速度，开始调用 BoardEntity 的位移协程
            _activeEntity.SetMoveAnimationSpeed(1f);
            StartCoroutine(_activeEntity.MoveAlongConnection(connection));
        }

        /// <summary>
        /// 监听事件总线发来的消息：实体已走完当前路段并成功到达下一个地块。
        /// </summary>
        private void OnEntitySegmentCompleted(BoardEntitySegmentCompletedEvent evt)
        {
            // 过滤无效消息，只处理当前激活实体的反馈
            if (!_isMoving || _activeEntity == null) return;
            if (evt.Entity != _activeEntity) return;

            // [逻辑流转] 切换到抵达处理协程
            if (_arrivalRoutine != null) StopCoroutine(_arrivalRoutine);
            _arrivalRoutine = StartCoroutine(HandleSegmentCompleted(evt.Node));
        }

        /// <summary>
        /// 协程：处理一段路径走完后的事务（步数扣除、效果触发、循环推进）。
        /// </summary>
        private IEnumerator HandleSegmentCompleted(MapWaypoint node)
        {
            // 判断这步走完是不是骰子点数归零（终点）
            bool isFinalStep = _stepsRemaining <= 1;

            // 执行节点抵达的交互逻辑（如加金币、触发对话、遇到 NPC 拦截等）
            yield return HandleNodeArrival(node, isFinalStep);

            // 消耗一步
            _stepsRemaining--;

            // [扭曲格] 强制滑行仅在最终落点时生效，路过时丢弃
            if (_pendingForcedNextNodeId >= 0)
            {
                if (isFinalStep)
                {
                    // 最终落点：补充1步，方向锁留给 AdvanceToNextStep 读取
                    if (_stepsRemaining <= 0) _stepsRemaining = 1;
                }
                else
                {
                    // 路过：丢弃强制滑行（分叉过滤仍有效）
                    _pendingForcedNextNodeId = -1;
                }
            }

            // 消费格子请求的额外步数（前进格 / 后退格写入）
            if (_pendingExtraSteps != 0)
            {
                int extra = _pendingExtraSteps;
                _pendingExtraSteps = 0;
                if (extra > 0)
                {
                    // 前进格：追加步数，自然向前继续移动
                    _stepsRemaining += extra;
                }
                else
                {
                    // 后退格：重置为向后步数，然后通过原地掉头实现方向反转
                    _stepsRemaining = -extra;
                    if (IsAtDeadEnd(_activeEntity))
                    {
                        // 死胡同节点：正向出口即来路，沿用首步掉头机制原路返回
                        _allowFirstStepUTurn = true;
                    }
                    else
                    {
                        // 普通节点：原地掉头，使 GetValidNextNodes 自然返回反向路径
                        _activeEntity.ReverseDirection();
                    }
                }
            }

            // 消费方向格请求（首步强制方向 + 指定步数，仅最终落点生效；路过时丢弃）
            if (_pendingDirectionalSteps > 0)
            {
                if (isFinalStep)
                {
                    _stepsRemaining          = _pendingDirectionalSteps;
                    _pendingForcedNextNodeId = _pendingDirectionalNodeId;
                }
                _pendingDirectionalSteps  = 0;
                _pendingDirectionalNodeId = -1;
            }

            // [人体大炮] 弹射仅在最终落点生效；路过时丢弃
            // 使用 while 循环支持连续弹射（落点也是大炮格时继续触发）
            if (_pendingCannonLaunch && isFinalStep)
            {
                while (_pendingCannonLaunch)
                {
                    _pendingCannonLaunch = false;
                    yield return DoCannonLaunch();
                }
                // 大炮落点可能触发了其他格子效果，依次检查并处理
                if (_pendingTeleport && !_isTeleporting)
                {
                    _pendingTeleport = false;
                    _isTeleporting = true;
                    yield return DoTeleport();
                    _isTeleporting = false;
                    FinishMove();
                    yield break;
                }
                if (_pendingDirectionalSteps > 0)
                {
                    _stepsRemaining          = _pendingDirectionalSteps;
                    _pendingForcedNextNodeId = _pendingDirectionalNodeId;
                    _pendingDirectionalSteps  = 0;
                    _pendingDirectionalNodeId = -1;
                    AdvanceToNextStep();
                    yield break;
                }
                FinishMove();
                yield break;
            }
            _pendingCannonLaunch = false; // 路过时丢弃

            // [传送格] 传送仅在最终落点生效；路过或已在传送中时丢弃（不连锁）
            if (_pendingTeleport && isFinalStep && !_isTeleporting)
            {
                _pendingTeleport = false;
                _isTeleporting = true;
                yield return DoTeleport();
                _isTeleporting = false;
                FinishMove();
                yield break;
            }
            _pendingTeleport = false; // 路过时丢弃

            if (_stepsRemaining <= 0)
            {
                FinishMove();
                yield break;
            }

            // 还没走完，继续寻找下一个目标节点
            AdvanceToNextStep();
        }

        /// <summary>
        /// 桥接逻辑：调用 InteractionHandler 来处理复杂的节点业务逻辑。
        /// </summary>
        private IEnumerator HandleNodeArrival(MapWaypoint node, bool isFinalStep)
        {
            if (_activeEntity == null) yield break;
            EnsureInteractionHandler();

            // 将节点事件的触发逻辑外包给专门的 Handler，使 Controller 保持整洁
            yield return _interactionHandler.HandleArrival(_activeEntity, node, isFinalStep, _triggerNodeEvents);
        }

        /// <summary>
        /// 原地掉头：反转玩家当前行进方向（供技能系统等外部逻辑复用）。
        /// 调用后下一次移动将朝反方向行进；若当前在死胡同则等效于允许首步掉头。
        /// </summary>
        public void ReversePlayerDirection()
        {
            if (_playerEntity == null) return;
            if (IsAtDeadEnd(_playerEntity))
            {
                _allowFirstStepUTurn = true;
            }
            else
            {
                _playerEntity.ReverseDirection();
            }
        }

        /// <summary>
        /// 立即停止移动并清空剩余步数（用于弹窗/切场景等非阻塞交互）。
        /// </summary>
        public void StopMoveImmediate()
        {
            _stepsRemaining = 0;
            FinishMove();
        }

        /// <summary>
        /// 结束整个移动流程：清理状态、发送事件、重置动画。
        /// </summary>
        private void FinishMove()
        {
            if (!_isMoving) return;

            _isMoving = false;
            if (_arrivalRoutine != null)
            {
                StopCoroutine(_arrivalRoutine);
                _arrivalRoutine = null;
            }
            UnsubscribeSegmentEvent();

            if (_activeEntity != null)
            {
                // 停止行走动画，设置实体为非移动状态（触发 Idle 动画）
                _activeEntity.SetMoveAnimationSpeed(0f);
                _activeEntity.SetMovingState(false);
            }

            // 广播全局事件：移动已结束（供主状态机监听，切回玩家待机或结算状态）
            EventBus.Raise(new BoardMovementEndedEvent { Entity = _activeEntity });
            _activeEntity = null;
        }

        /// <summary>
        /// NPC 的自动寻路策略：从多个有效出口中随机选择一个。
        /// </summary>
        private WaypointConnection ChooseNextConnection(MapWaypoint node, List<MapWaypoint> validNodes)
        {
            if (node == null || validNodes == null || validNodes.Count == 0) return null;
            if (validNodes.Count == 1) return node.GetConnectionTo(validNodes[0]);

            // 简单的随机决策，未来可在此扩展复杂的 AI 权重计算
            int index = UnityEngine.Random.Range(0, validNodes.Count);
            return node.GetConnectionTo(validNodes[index]);
        }

        /// <summary>
        /// 判断实体当前是否停在死胡同（唯一可走方向是原路返回）。
        /// 用于 BeginMove 时决定是否允许首步掉头。
        /// </summary>
        private bool IsAtDeadEnd(BoardEntity entity)
        {
            if (entity == null || entity.CurrentNode == null) return false;
            List<MapWaypoint> validNodes = entity.CurrentNode.GetValidNextNodes(entity.LastWaypoint);
            return validNodes.Count == 1 && entity.LastWaypoint != null && validNodes[0] == entity.LastWaypoint;
        }

        /// <summary>
        /// 确保交互处理器已实例化（懒加载模式）。
        /// </summary>
        private void EnsureInteractionHandler()
        {
            if (_interactionHandler != null) return;
            _interactionHandler = new BoardInteractionHandler();
        }

        /// <summary>
        /// 执行炮弹弹射：随机选目标节点 → 抛物线飞行 → 触发目标格子效果。
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

            yield return _activeEntity.LaunchParabolic(target, _pendingCannonArcHeight, _pendingCannonLaunchSpeed);

            // 触发落点格子效果（作为最终落点处理）
            yield return HandleNodeArrival(target, true);
        }

        /// <summary>
        /// 接收人体大炮弹射请求事件（仅在移动期间订阅）。
        /// </summary>
        private void OnCannonLaunchRequested(BoardCannonLaunchRequestedEvent evt)
        {
            _pendingCannonLaunch = true;
            _pendingCannonArcHeight = evt.ArcHeight;
            _pendingCannonLaunchSpeed = evt.LaunchSpeed;
        }

        /// <summary>
        /// 执行传送：按指定节点 ID 瞬间移动玩家 → 触发目标格子效果。
        /// </summary>
        private IEnumerator DoTeleport()
        {
            MapWaypoint target = BoardMapManager.Instance != null
                ? BoardMapManager.Instance.GetNode(_pendingTeleportTargetId)
                : null;

            if (target == null)
            {
                DebugTools.LogWarning($"[Teleport Tile] 找不到节点 ID={_pendingTeleportTargetId}，跳过传送。");
                yield break;
            }

            DebugTools.Log($"<color=cyan>[Teleport Tile]</color> 传送目标：{target.nodeID} ({target.name})");
            // 瞬间传送：snap 坐标 + 重置来路（防止传送后被当成原路返回）
            _activeEntity.SetCurrentNode(target, true, true);

            yield return HandleNodeArrival(target, true);
        }

        /// <summary>
        /// 接收方向格移动请求事件（仅在移动期间订阅）。
        /// </summary>
        private void OnDirectionalMoveRequested(BoardDirectionalMoveRequestedEvent evt)
        {
            _pendingDirectionalNodeId = evt.DirectionNodeId;
            _pendingDirectionalSteps  = evt.Steps;
        }

        /// <summary>
        /// 接收传送格传送请求事件（仅在移动期间订阅）。
        /// </summary>
        private void OnTeleportRequested(BoardTeleportRequestedEvent evt)
        {
            _pendingTeleport         = true;
            _pendingTeleportTargetId = evt.TargetNodeId;
        }

        /// <summary>
        /// 接收格子请求的额外步数事件（仅在移动期间订阅）。
        /// </summary>
        private void OnExtraMoveRequested(BoardExtraMoveRequestedEvent evt)
        {
            _pendingExtraSteps = evt.Steps;
        }

        /// <summary>
        /// 接收扭曲格的强制滑行请求（仅在移动期间订阅）。
        /// </summary>
        private void OnWarpSlideRequested(BoardWarpSlideRequestedEvent evt)
        {
            _pendingForcedNextNodeId = evt.ForcedNodeId;
        }

        /// <summary>
        /// 接收扭曲格的路径过滤请求（仅在移动期间订阅）。
        /// </summary>
        private void OnWarpFilterPathRequested(BoardWarpFilterPathEvent evt)
        {
            _pendingProtectedNodeId = evt.ProtectedNodeId;
        }

        // --- 事件总线订阅管理 ---
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
