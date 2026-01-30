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

        public event Action<MapWaypoint, List<WaypointConnection>, Action<WaypointConnection>> ForkSelectionRequested;

        private BoardEntity _playerEntity;
        private BoardEntity _activeEntity;
        private MapWaypoint _startNode;
        private bool _isMoving = false;
        private bool _triggerNodeEvents = true;
        private BoardInteractionHandler _interactionHandler;
        private int _stepsRemaining;
        private Coroutine _arrivalRoutine;
        private bool _waitingForFork;

        private void OnDisable()
        {
            // 关闭时强制停止移动与协程，避免残留状态
            StopAllCoroutines();
            UnsubscribeSegmentEvent();
            _isMoving = false;
            _waitingForFork = false;
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
            _stepsRemaining = totalSteps;
            _waitingForFork = false;

            // 同步到实体层，确保事件在连接上触发
            _activeEntity.TriggerConnectionEvents = triggerNodeEvents;
            _activeEntity.SetMovingState(true);
            _activeEntity.SetMoveAnimationSpeed(1f);

            _isMoving = true;
            SubscribeSegmentEvent();
            AdvanceToNextStep();
        }

        public void ResetToStart()
        {
            StopAllCoroutines();
            _isMoving = false;
            _waitingForFork = false;
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

        private void AdvanceToNextStep()
        {
            if (!_isMoving || _activeEntity == null)
            {
                FinishMove();
                return;
            }

            if (_stepsRemaining <= 0)
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

            List<MapWaypoint> validNodes = current.GetValidNextNodes(_activeEntity.LastWaypoint);
            if (validNodes.Count == 0)
            {
                FinishMove();
                return;
            }

            if (validNodes.Count == 1)
            {
                WaypointConnection conn = current.GetConnectionTo(validNodes[0]);
                StartSegment(conn);
                return;
            }

            List<WaypointConnection> options = current.GetConnectionsTo(validNodes);
            if (ForkSelectionRequested != null)
            {
                _waitingForFork = true;
                _activeEntity.SetMoveAnimationSpeed(0f);
                ForkSelectionRequested.Invoke(current, options, result =>
                {
                    _waitingForFork = false;
                    StartSegment(result);
                });
                return;
            }

            StartSegment(ChooseNextConnection(current, validNodes));
        }

        private void StartSegment(WaypointConnection connection)
        {
            if (!_isMoving || _activeEntity == null)
            {
                FinishMove();
                return;
            }

            if (connection == null)
            {
                FinishMove();
                return;
            }

            _activeEntity.SetMoveAnimationSpeed(1f);
            StartCoroutine(_activeEntity.MoveAlongConnection(connection));
        }

        private void OnEntitySegmentCompleted(BoardEntitySegmentCompletedEvent evt)
        {
            if (!_isMoving || _activeEntity == null) return;
            if (evt.Entity != _activeEntity) return;

            if (_arrivalRoutine != null)
            {
                StopCoroutine(_arrivalRoutine);
            }
            _arrivalRoutine = StartCoroutine(HandleSegmentCompleted(evt.Node));
        }

        private IEnumerator HandleSegmentCompleted(MapWaypoint node)
        {
            bool isFinalStep = _stepsRemaining <= 1;
            yield return HandleNodeArrival(node, isFinalStep);

            _stepsRemaining--;
            if (_stepsRemaining <= 0)
            {
                FinishMove();
                yield break;
            }

            AdvanceToNextStep();
        }

        private IEnumerator HandleNodeArrival(MapWaypoint node, bool isFinalStep)
        {
            if (_activeEntity == null) yield break;
            EnsureInteractionHandler();
            // 节点事件由交互处理器统一负责
            yield return _interactionHandler.HandleArrival(_activeEntity, node, isFinalStep, _triggerNodeEvents);
        }

        private void FinishMove()
        {
            if (!_isMoving) return;

            _isMoving = false;
            _waitingForFork = false;
            if (_arrivalRoutine != null)
            {
                StopCoroutine(_arrivalRoutine);
                _arrivalRoutine = null;
            }
            UnsubscribeSegmentEvent();

            if (_activeEntity != null)
            {
                _activeEntity.SetMoveAnimationSpeed(0f);
                _activeEntity.SetMovingState(false);
            }

            EventBus.Raise(new BoardMovementEndedEvent { Entity = _activeEntity });
            _activeEntity = null;
        }

        private WaypointConnection ChooseNextConnection(MapWaypoint node, List<MapWaypoint> validNodes)
        {
            if (node == null || validNodes == null || validNodes.Count == 0) return null;
            if (validNodes.Count == 1) return node.GetConnectionTo(validNodes[0]);
            int index = UnityEngine.Random.Range(0, validNodes.Count);
            return node.GetConnectionTo(validNodes[index]);
        }

        private void EnsureInteractionHandler()
        {
            if (_interactionHandler != null) return;
            _interactionHandler = new BoardInteractionHandler();
        }

        private void SubscribeSegmentEvent()
        {
            EventBus.Subscribe<BoardEntitySegmentCompletedEvent>(OnEntitySegmentCompleted);
        }

        private void UnsubscribeSegmentEvent()
        {
            EventBus.Unsubscribe<BoardEntitySegmentCompletedEvent>(OnEntitySegmentCompleted);
        }
    }
}
