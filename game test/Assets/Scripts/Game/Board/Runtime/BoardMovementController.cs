using System;
using System.Collections;
using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
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

        private void Awake()
        {
            _startNode = FindStartNode();
            Debug.Log("[BoardMovementController] Start Node ID: " + (_startNode != null ? _startNode.nodeID.ToString() : "null"));
        }

        private void Start()
        {
            if (!IsBoardModeActive()) return;

            if (playerToken != null)
            {
                CachePlayerEntity();
                return;
            }

            ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
        }

        private void OnDisable()
        {
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
                ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
                entity = _playerEntity;
                if (entity == null) return;
            }
            _activeEntity = entity;
            _triggerNodeEvents = triggerNodeEvents;
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
                ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
                entity = _playerEntity;
                if (entity == null) return;
            }

            _activeEntity = entity;
            _triggerNodeEvents = triggerNodeEvents;
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
            if (_startNode != null && _playerEntity != null)
            {
                _playerEntity.SetCurrentNode(_startNode, true);
            }
        }

        public void SetCurrentNodeById(int nodeId)
        {
            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (_playerEntity == null) CachePlayerEntity();
            if (_playerEntity == null) return;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].nodeID != nodeId) continue;
                _playerEntity.SetCurrentNode(nodes[i], true);
                return;
            }
        }

        public void ResolveReferences(int preferredNodeId)
        {
            _startNode = FindStartNode();
            playerToken = GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null
                ? GameManager.Instance.CurrentPlayer.transform
                : null;
            CachePlayerEntity();

            if (preferredNodeId >= 0)
            {
                SetCurrentNodeById(preferredNodeId);
                return;
            }

            if (_playerEntity != null && _playerEntity.CurrentNode == null && _startNode != null)
            {
                _playerEntity.SetCurrentNode(_startNode, true);
            }
        }

        private MapWaypoint FindStartNode()
        {
            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].nodeID == 0) return nodes[i];
            }
            return null;
        }

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
                ForkSelectionRequested.Invoke(currentNode, result =>
                {
                    selectedConnection = result;
                    selectionResolved = true;
                });
                yield return new WaitUntil(() => selectionResolved || !isActiveAndEnabled);
            }
            else
            {
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
            if (!_triggerNodeEvents) yield break;
            if (node == null || node.tileData == null) yield break;

            BoardEntity other = BoardEntity.FindOtherAtNode(node, _activeEntity);
            if (other != null)
            {
                _activeEntity.SetMoveAnimationSpeed(0f);
                yield return StartCoroutine(HandleEntityEncounter(other, node));
                if (!isFinalStep) _activeEntity.SetMoveAnimationSpeed(1f);
            }

            bool shouldTrigger = isFinalStep || node.tileData.TriggerOnPass;

            if (shouldTrigger) _activeEntity.SetMoveAnimationSpeed(0f);

            if (shouldTrigger)
            {
                EventBus.Raise(new PlayerReachedNodeEvent { Node = node });
                node.tileData.OnEnter(_activeEntity.gameObject);
            }

            if (ConfirmationEvent.HasPending)
            {
                bool responded = false;
                void OnResponded(bool _) => responded = true;
                ConfirmationEvent.OnResponded += OnResponded;
                while (!responded)
                {
                    yield return null;
                }
                ConfirmationEvent.OnResponded -= OnResponded;
            }

            if (!isFinalStep) _activeEntity.SetMoveAnimationSpeed(1f);
        }

        private IEnumerator HandleEntityEncounter(BoardEntity other, MapWaypoint node)
        {
            bool completed = false;
            BoardEntityInteractionEvent evt = new BoardEntityInteractionEvent
            {
                Player = _activeEntity,
                Target = other,
                Node = node,
                OnCompleted = () => completed = true
            };

            if (!EventBus.HasSubscribers<BoardEntityInteractionEvent>())
            {
                completed = true;
            }
            EventBus.Raise(evt);
            while (!completed)
            {
                yield return null;
            }
        }

        private bool IsBoardModeActive()
        {
            return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode;
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
    }
}
