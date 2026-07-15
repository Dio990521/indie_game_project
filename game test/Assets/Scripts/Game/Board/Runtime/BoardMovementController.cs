using IndieGame.Core.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Gameplay.Board.FogOfWar;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘位移控制器：负责驱动棋盘实体（玩家或NPC）在地图上的步进式移动。
    /// 它是逻辑中枢，连接了地图数据（MapWaypoint）、实体表现（BoardEntity）和交互逻辑（BoardInteractionHandler）。
    /// <para>
    /// 本类用 partial 关键字拆分到多个文件以降低单文件长度：
    /// - 本文件：核心移动逻辑（BeginMove / AdvanceToNextStep / HandleSegmentCompleted / FinishMove 等）；
    /// - <c>BoardMovementController.Effects.cs</c>：格子效果事件订阅 + 大炮/传送等特效协程。
    /// 字段与公开 API 都保留在本主文件，便于一眼看到所有状态。
    /// </para>
    /// </summary>
    public partial class BoardMovementController : MonoBehaviour
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
        // 特殊格子效果的待执行状态集合（详见 TileEffectPendingState）
        private TileEffectPendingState _fx = TileEffectPendingState.Default;
        // [传送格] 防止连锁传送标志（不属于 pending，在协程执行期间持续有效）
        private bool _isTeleporting = false;
        // [不动铃铛] 激活后，本次移动结束前所有位移格效果将被忽略
        private bool _immovableBellActive = false;
        // [影骰子] 激活后，下一次掷骰子点数翻倍，消耗后自动清除
        private bool _shadowDiceActive = false;
        // [人体大炮] 落地时预选的首步方向节点ID。跨移动序列持久，在下次BeginMove首步消耗后清除。
        // 确保落地后下次投骰直接移动，不弹出岔路选择UI。-1表示未激活。
        private int _cannonPresetFirstStepNodeId = -1;

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
            _activeEntity      = entity;
            _triggerNodeEvents = triggerNodeEvents;
            _stepsRemaining    = totalSteps;
            _fx                = TileEffectPendingState.Default;
            ComboMoveSystem.ResetCombo(); // 每次掷骰开始时清零连锁计数

            // [人体大炮] 消耗上一次弹射落地时预选的首步方向，注入 ForcedNextNodeId。
            // AdvanceToNextStep 首次调用时会读取该值并跳过岔路UI直接走向预设节点，消耗后清除。
            // 仅对玩家实体生效：NPC 移动不应消费玩家的炮弹落地朝向，否则方向锁会错误注入 NPC 路径。
            if (_cannonPresetFirstStepNodeId >= 0 && entity == _playerEntity)
            {
                _fx.ForcedNextNodeId       = _cannonPresetFirstStepNodeId;
                _cannonPresetFirstStepNodeId = -1;
            }

            // 同步底层属性：实体在移动时是否检测路径上的事件（如连线中间的交互）
            _activeEntity.TriggerConnectionEvents = triggerNodeEvents;
            _activeEntity.SetMovingState(true);
            _activeEntity.SetMoveAnimationSpeed(1f);

            _isMoving = true;

            // 检测当前实体是否停在死胡同。若是，允许本次移动的首步执行掉头（掉头机制）
            _allowFirstStepUTurn = TileEffectApplier.IsAtDeadEnd(entity);

            // 订阅”路段完成”事件，用于在两点之间移动完后执行决策
            SubscribeSegmentEvent();
            // 开始推进第一步
            AdvanceToNextStep();
        }

        /// <summary>
        /// 强制重置控制器：停止一切移动并将玩家放回起点。
        /// H1 修复：中断移动时必须退订格子事件并复位效果状态。
        /// 旧实现只置 _isMoving=false，7 个 EventBus 订阅残留，下次 BeginMove 再订阅
        /// 会造成委托链重复（EventBus 不去重），每个格子事件被处理两次。
        /// </summary>
        public void ResetToStart()
        {
            StopAllCoroutines();

            if (_isMoving)
            {
                // 与 FinishMove 相同的收尾，但不广播 BoardMovementEndedEvent：
                // 重置属于"强制作废本次移动"，不应触发"移动正常结束"的状态机流转。
                UnsubscribeSegmentEvent();
                _arrivalRoutine = null; // 协程已被 StopAllCoroutines 终止
                if (_activeEntity != null)
                {
                    _activeEntity.SetMoveAnimationSpeed(0f);
                    _activeEntity.SetMovingState(false);
                }
                _activeEntity = null;
            }

            _isMoving = false;
            // 清空所有待执行的格子效果与一次性标志，防止残留到下一次移动
            _fx = TileEffectPendingState.Default;
            _immovableBellActive = false;
            _shadowDiceActive = false;
            _isTeleporting = false;
            _allowFirstStepUTurn = false;
            _cannonPresetFirstStepNodeId = -1;

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
            if (_fx.ProtectedNodeId >= 0)
            {
                MapWaypoint protectedNode = BoardMapManager.Instance != null
                    ? BoardMapManager.Instance.GetNode(_fx.ProtectedNodeId)
                    : null;
                if (protectedNode != null) validNodes.Remove(protectedNode);
                _fx.ProtectedNodeId = -1;
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
            if (_fx.ForcedNextNodeId >= 0)
            {
                int forcedId        = _fx.ForcedNextNodeId;
                _fx.ForcedNextNodeId = -1;
                _fx.ProtectedNodeId  = -1; // 强制方向时无需过滤，同步清除

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
        /// 主流程已拆分为多个 ApplyXxx / ProcessXxx 子方法，提升可读性。
        /// </summary>
        private IEnumerator HandleSegmentCompleted(MapWaypoint node)
        {
            // 判断这步走完是不是骰子点数归零（终点）
            bool isFinalStep = _stepsRemaining <= 1;

            // 执行节点抵达的交互逻辑（如加金币、触发对话、遇到 NPC 拦截等）
            yield return HandleNodeArrival(node, isFinalStep);

            // 消耗一步
            _stepsRemaining--;

            // [不动铃铛] 激活时：清除所有位移类效果，强制停在骰子结果格
            if (_immovableBellActive)
            {
                TileEffectApplier.ClearAllMovementEffects(ref _fx);

                if (_stepsRemaining <= 0)
                {
                    _immovableBellActive = false; // 一次性消耗
                    FinishMove();
                    yield break;
                }
                AdvanceToNextStep();
                yield break;
            }

            // 各类格子状态效果（不涉及 yield 的部分）按顺序处理。
            // 这三步从原 ApplyXxxEffect 私有方法重构为 TileEffectApplier 的静态调用，
            // 主流程更短，且每个 Apply 方法独立可测试。
            TileEffectApplier.ApplyForcedNext(ref _fx, ref _stepsRemaining, isFinalStep);
            TileEffectApplier.ApplyExtraSteps(ref _fx, ref _stepsRemaining, _activeEntity, out bool needFirstStepUTurn);
            if (needFirstStepUTurn) _allowFirstStepUTurn = true;
            TileEffectApplier.ApplyDirectionalSteps(ref _fx, ref _stepsRemaining, isFinalStep);

            // [人体大炮] 涉及协程 + 链式触发，单独抽出处理；返回 true 表示流程已自行结束
            if (_fx.CannonLaunch && isFinalStep)
            {
                yield return ProcessCannonChainCoroutine();
                yield break;
            }
            _fx.CannonLaunch = false; // 路过时丢弃

            // [传送格] 仅在最终落点生效
            if (_fx.Teleport && isFinalStep && !_isTeleporting)
            {
                yield return ExecuteTeleportRoutine();
                FinishMove();
                yield break;
            }
            _fx.Teleport = false; // 路过时丢弃

            if (_stepsRemaining <= 0)
            {
                FinishMove();
                yield break;
            }

            // 还没走完，继续寻找下一个目标节点
            AdvanceToNextStep();
        }

        // 注：ProcessCannonChainCoroutine / ExecuteTeleportRoutine 已迁移至
        // BoardMovementController.Effects.cs（同一 partial class），主文件保持精简。

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
        /// 激活不动铃铛效果：下一次移动将忽略所有位移格效果（大炮、传送、行进、扭曲格强制滑行等），
        /// 强制玩家停在骰子点数对应的格子上。效果在移动结束时自动消耗。
        /// </summary>
        public void ActivateImmovableBell() => _immovableBellActive = true;

        /// <summary>
        /// 激活影骰子效果：下一次掷骰子点数翻倍，消耗后自动清除。
        /// </summary>
        public void ActivateShadowDice() => _shadowDiceActive = true;

        /// <summary>
        /// 消耗影骰子效果：若激活则清除标志并返回 true，否则返回 false。
        /// 由 PlayerTurnState 在掷骰后调用，原子性地检测并重置标志。
        /// </summary>
        public bool ConsumeShadowDice()
        {
            if (!_shadowDiceActive) return false;
            _shadowDiceActive = false;
            return true;
        }

        /// <summary>
        /// 原地掉头：反转玩家当前行进方向（供技能系统等外部逻辑复用）。
        /// 调用后下一次移动将朝反方向行进；若当前在死胡同则等效于允许首步掉头。
        /// </summary>
        public void ReversePlayerDirection()
        {
            if (_playerEntity == null) return;
            if (TileEffectApplier.IsAtDeadEnd(_playerEntity))
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
            // 保险清除：防止 StopMoveImmediate 等异常路径导致标志残留
            _immovableBellActive = false;
            _shadowDiceActive = false;
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

        // 注：原 private bool IsAtDeadEnd(BoardEntity entity) 已移至 TileEffectApplier.IsAtDeadEnd，
        // BeginMove / ReversePlayerDirection / TileEffectApplier.ApplyExtraSteps 均改为调用该静态方法。

        /// <summary>
        /// 确保交互处理器已实例化（懒加载模式）。
        /// </summary>
        private void EnsureInteractionHandler()
        {
            if (_interactionHandler != null) return;
            _interactionHandler = new BoardInteractionHandler();
        }

        // 注：以下方法均已迁移至 BoardMovementController.Effects.cs（同一 partial class）：
        // - 特效协程：DoCannonLaunch / DoTeleport / ProcessCannonChainCoroutine / ExecuteTeleportRoutine
        // - 事件订阅处理器：OnCannonLaunchRequested / OnTeleportRequested / OnDirectionalMoveRequested /
        //                   OnExtraMoveRequested / OnWarpSlideRequested / OnWarpFilterPathRequested

        // 注：以下成员已迁往 BoardMovementController.Effects.cs（同一 partial class）：
        // - HandleExternalArrival（外部跳跃落点的完整格子效果管线，飞翼宝具等使用）
        // - SubscribeSegmentEvent / UnsubscribeSegmentEvent（格子效果事件的订阅管理）
    }
}
