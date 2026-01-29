using System;
using System.Collections;
using System.Collections.Generic;
using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardMovementController : MonoBehaviour
    {
        [Header("Dependencies")]
        public BoardForkSelector forkSelector;

        [Header("Game References")]
        public Transform playerToken;

        public bool IsMoving => _isMoving;
        public int CurrentNodeId => _playerEntity != null && _playerEntity.CurrentNode != null
            ? _playerEntity.CurrentNode.nodeID
            : -1;
        public BoardEntity PlayerEntity => _playerEntity;
        public event Action MoveStarted;
        public event Action MoveEnded;
        public event Action<MapWaypoint, Action<WaypointConnection>> ForkSelectionRequested;

        private BoardEntity _playerEntity;
        private BoardEntity _activeEntity;
        private MapWaypoint _startNode;
        private bool _isMoving = false;
        private bool _triggerNodeEvents = true;
        private BoardInteractionHandler _interactionHandler;

        private void OnDisable()
        {
            // 关闭时强制停止移动与协程，避免残留状态
            StopAllCoroutines();
            _isMoving = false;
            if (_activeEntity != null)
            {
                _activeEntity.SetMoveAnimationSpeed(0f);
                _activeEntity.SetMovingState(false);
            }
        }

        public void BeginMove(int totalSteps)
        {
            BeginMove(_playerEntity, totalSteps, true);
        }

        public void BeginMove(BoardEntity entity, int totalSteps, bool triggerNodeEvents = true)
        {
            if (_isMoving) return;
            if (entity == null)
            {
                // 尝试修复引用并回退到玩家实体
                ResolveReferences(-1);
                entity = _playerEntity;
                if (entity == null) return;
            }
            _activeEntity = entity;
            _triggerNodeEvents = triggerNodeEvents;
            // 同步到实体层，确保事件在连接上触发
            _activeEntity.TriggerConnectionEvents = triggerNodeEvents;
            _isMoving = true;
            _activeEntity.SetMovingState(true);
            MoveStarted?.Invoke();
            StartCoroutine(MoveRoutine(totalSteps));
        }

        public void BeginDirectedMove(BoardEntity entity, bool triggerNodeEvents = true)
        {
            if (_isMoving) return;
            if (entity == null)
            {
                // 没有显式传入实体时，优先用玩家实体
                ResolveReferences(-1);
                entity = _playerEntity;
                if (entity == null) return;
            }

            _activeEntity = entity;
            _triggerNodeEvents = triggerNodeEvents;
            // DirectedMove 由外部按步驱动，但事件触发逻辑一致
            _activeEntity.TriggerConnectionEvents = triggerNodeEvents;
            _isMoving = true;
            _activeEntity.SetMovingState(true);
            MoveStarted?.Invoke();
        }

        public void EndDirectedMove()
        {
            if (!_isMoving) return;
            _isMoving = false;
            if (_activeEntity != null)
            {
                _activeEntity.SetMoveAnimationSpeed(0f);
                _activeEntity.SetMovingState(false);
            }
            MoveEnded?.Invoke();
            _activeEntity = null;
        }

        public IEnumerator MoveActiveEntityAlongConnection(WaypointConnection connection, bool isFinalStep)
        {
            if (_activeEntity == null || connection == null) yield break;
            // 逐段移动，保持与 BoardEntity 的曲线逻辑一致
            _activeEntity.SetMoveAnimationSpeed(1f);
            yield return StartCoroutine(_activeEntity.MoveAlongConnection(connection));
            yield return StartCoroutine(HandleNodeArrival(connection.targetNode, isFinalStep));
            if (isFinalStep && _activeEntity != null) _activeEntity.SetMoveAnimationSpeed(0f);
        }

        public void ResetToStart()
        {
            StopAllCoroutines();
            _isMoving = false;
            if (forkSelector != null) forkSelector.ClearSelection();
            // 重置到起点只改变玩家实体位置
            if (_startNode != null && _playerEntity != null)
            {
                _playerEntity.SetCurrentNode(_startNode, true);
            }
        }

        public void SetCurrentNodeById(int nodeId)
        {
            if (_playerEntity == null) CachePlayerEntity();
            if (_playerEntity == null) return;
            MapWaypoint node = BoardMapManager.Instance != null ? BoardMapManager.Instance.GetNode(nodeId) : null;
            if (node == null) return;
            _playerEntity.SetCurrentNode(node, true);
        }

        public void ResolveReferences(int preferredNodeId)
        {
            EnsureInteractionHandler();
            if (BoardMapManager.Instance != null && !BoardMapManager.Instance.IsReady)
            {
                // 保证地图节点缓存就绪
                BoardMapManager.Instance.Init();
            }
            _startNode = BoardMapManager.Instance != null ? BoardMapManager.Instance.GetNode(0) : null;
            playerToken = GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null
                ? GameManager.Instance.CurrentPlayer.transform
                : null;
            CachePlayerEntity();

            if (preferredNodeId >= 0)
            {
                // 外部指定起始节点时优先使用
                SetCurrentNodeById(preferredNodeId);
                return;
            }

            if (_playerEntity != null && _playerEntity.CurrentNode == null && _startNode != null)
            {
                // 首次进入时将玩家放到起点
                _playerEntity.SetCurrentNode(_startNode, true);
            }
        }

        private void CachePlayerEntity()
        {
            _playerEntity = null;
            if (playerToken == null) return;
            _playerEntity = playerToken.GetComponent<BoardEntity>();
            if (_playerEntity == null)
            {
                // 玩家物体缺少 BoardEntity 时自动补齐
                _playerEntity = playerToken.gameObject.AddComponent<BoardEntity>();
                _playerEntity.SetAsPlayer(true);
            }
        }

        private class StepContext
        {
            public int StepsRemaining;
        }

        private IEnumerator MoveRoutine(int totalSteps)
        {
            StepContext ctx = new StepContext { StepsRemaining = totalSteps };
            if (_activeEntity != null) _activeEntity.SetMoveAnimationSpeed(1f);
            while (ctx.StepsRemaining > 0)
            {
                // 每步独立处理，可能在分叉点暂停
                yield return StartCoroutine(ProcessStep(ctx));
            }

            if (_activeEntity != null) _activeEntity.SetMoveAnimationSpeed(0f);
            _isMoving = false;
            if (_activeEntity != null) _activeEntity.SetMovingState(false);
            MoveEnded?.Invoke();
        }

        private IEnumerator ProcessStep(StepContext ctx)
        {
            List<WaypointConnection> path = new List<WaypointConnection>();
            bool encounteredFork = false;
            MapWaypoint tempNode = _activeEntity != null ? _activeEntity.CurrentNode : null;
            MapWaypoint tempLast = _activeEntity != null ? _activeEntity.LastWaypoint : null;
            if (tempNode == null)
            {
                ctx.StepsRemaining = 0;
                yield break;
            }

            for (int i = 0; i < ctx.StepsRemaining; i++)
            {
                System.Collections.Generic.List<MapWaypoint> validNodes = tempNode.GetValidNextNodes(tempLast);
                if (validNodes.Count == 0)
                {
                    ctx.StepsRemaining = 0;
                    break;
                }
                if (validNodes.Count == 1)
                {
                    // 只有一条路时直接加入路径
                    WaypointConnection conn = tempNode.GetConnectionTo(validNodes[0]);
                    if (conn == null)
                    {
                        ctx.StepsRemaining = 0;
                        break;
                    }
                    path.Add(conn);
                    tempLast = tempNode;
                    tempNode = conn.targetNode;
                }
                else
                {
                    // 遇到分叉时停止路径收集，交由选择流程
                    encounteredFork = true;
                    break;
                }
            }

            if (path.Count > 0)
            {
                yield return StartCoroutine(MoveSegmentPath(path, ctx));
            }

            if (encounteredFork && ctx.StepsRemaining > 0)
            {
                yield return StartCoroutine(HandleFork(ctx));
            }
        }

        private IEnumerator MoveSegmentPath(List<WaypointConnection> path, StepContext ctx)
        {
            if (_activeEntity != null) _activeEntity.SetMoveAnimationSpeed(1f);
            foreach (var conn in path)
            {
                if (_activeEntity == null) yield break;
                yield return StartCoroutine(_activeEntity.MoveAlongConnection(conn));
                ctx.StepsRemaining--;
                yield return StartCoroutine(HandleNodeArrival(conn.targetNode, ctx.StepsRemaining == 0));
            }
            if (_activeEntity != null) _activeEntity.SetMoveAnimationSpeed(0f);
        }

        private IEnumerator HandleFork(StepContext ctx)
        {
            WaypointConnection selectedConnection = null;
            bool selectionResolved = false;
            MapWaypoint currentNode = _activeEntity != null ? _activeEntity.CurrentNode : null;
            if (currentNode == null) yield break;
            if (_activeEntity != null) _activeEntity.SetMoveAnimationSpeed(0f);

            if (ForkSelectionRequested != null)
            {
                // 由外部 UI/输入系统决定分叉路线
                ForkSelectionRequested.Invoke(currentNode, result =>
                {
                    selectedConnection = result;
                    selectionResolved = true;
                });
                yield return new WaitUntil(() => selectionResolved || !isActiveAndEnabled);
            }
            else
            {
                // 没有监听者则随机选择
                selectedConnection = ChooseNextConnection(currentNode);
            }

            if (!isActiveAndEnabled || selectedConnection == null) yield break;

            yield return new WaitForSeconds(0.2f);
            if (_activeEntity != null) _activeEntity.SetMoveAnimationSpeed(1f);
            if (_activeEntity == null) yield break;
            yield return StartCoroutine(_activeEntity.MoveAlongConnection(selectedConnection));
            ctx.StepsRemaining--;
            yield return StartCoroutine(HandleNodeArrival(selectedConnection.targetNode, ctx.StepsRemaining == 0));
        }

        private IEnumerator HandleNodeArrival(MapWaypoint node, bool isFinalStep)
        {
            if (_activeEntity == null) yield break;
            EnsureInteractionHandler();
            // 节点事件由交互处理器统一负责
            yield return _interactionHandler.HandleArrival(_activeEntity, node, isFinalStep, _triggerNodeEvents);
        }

        private WaypointConnection ChooseNextConnection(MapWaypoint node)
        {
            if (node == null || node.connections.Count == 0) return null;
            MapWaypoint last = _activeEntity != null ? _activeEntity.LastWaypoint : null;
            System.Collections.Generic.List<MapWaypoint> validNodes = node.GetValidNextNodes(last);
            if (validNodes.Count == 0) return null;
            if (validNodes.Count == 1) return node.GetConnectionTo(validNodes[0]);
            int index = UnityEngine.Random.Range(0, validNodes.Count);
            return node.GetConnectionTo(validNodes[index]);
        }

        private void EnsureInteractionHandler()
        {
            if (_interactionHandler != null) return;
            _interactionHandler = new BoardInteractionHandler();
        }
    }
}
