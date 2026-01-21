using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Events;
using IndieGame.Gameplay.Board.Data;
using IndieGame.UI.Confirmation;
using System;
using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardMovementController : MonoBehaviour
    {
        [Header("Dependencies")]
        public BoardForkSelector forkSelector;

        [Header("Game References")]
        public Transform playerToken;

        [Header("Settings")]
        public float moveSpeed = 5f;
        public float rotateSpeed = 15f;
        public string moveSpeedParamName = "Speed";

        public bool IsMoving => _isMoving;
        public int CurrentNodeId => _currentNode != null ? _currentNode.nodeID : -1;
        public event Action MoveStarted;
        public event Action MoveEnded;
        public event Action<MapWaypoint, Action<WaypointConnection>> ForkSelectionRequested;

        private int _animIDSpeed;
        private Animator _playerAnimator;
        private MapWaypoint _currentNode;
        private MapWaypoint _startNode;
        private bool _isMoving = false;

        private void Awake()
        {
            _animIDSpeed = Animator.StringToHash(moveSpeedParamName);
            _startNode = FindStartNode();
            Debug.Log("[BoardMovementController] Start Node ID: " + (_startNode != null ? _startNode.nodeID.ToString() : "null"));
        }

        private void Start()
        {
            if (!IsBoardModeActive()) return;

            if (playerToken != null)
            {
                CacheAnimator();
                return;
            }

            ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _isMoving = false;
        }

        public void BeginMove(int totalSteps)
        {
            if (_isMoving) return;
            if (playerToken == null)
            {
                ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
                if (playerToken == null) return;
            }
            _isMoving = true;
            MoveStarted?.Invoke();
            StartCoroutine(MoveRoutine(totalSteps));
        }

        public void ResetToStart()
        {
            StopAllCoroutines();
            _isMoving = false;
            if (forkSelector != null) forkSelector.ClearSelection();
            if (_startNode != null && playerToken != null)
            {
                _currentNode = _startNode;
                playerToken.position = _startNode.transform.position;
            }
        }

        public void SetCurrentNodeById(int nodeId)
        {
            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].nodeID != nodeId) continue;
                _currentNode = nodes[i];
                if (playerToken != null)
                {
                    playerToken.position = nodes[i].transform.position;
                }
                return;
            }
        }

        public void ResolveReferences(int preferredNodeId)
        {
            _startNode = FindStartNode();
            playerToken = GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null
                ? GameManager.Instance.CurrentPlayer.transform
                : null;
            CacheAnimator();

            if (preferredNodeId >= 0)
            {
                SetCurrentNodeById(preferredNodeId);
                return;
            }

            if (_currentNode == null && _startNode != null)
            {
                _currentNode = _startNode;
                if (playerToken != null)
                {
                    playerToken.position = _startNode.transform.position;
                }
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

        private void CacheAnimator()
        {
            _playerAnimator = null;
            if (playerToken == null) return;

            Animator[] animators = playerToken.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i].runtimeAnimatorController == null) continue;
                _playerAnimator = animators[i];
                return;
            }
        }

        private class StepContext
        {
            public int StepsRemaining;
        }

        private IEnumerator MoveRoutine(int totalSteps)
        {
            StepContext ctx = new StepContext { StepsRemaining = totalSteps };
            while (ctx.StepsRemaining > 0)
            {
                yield return StartCoroutine(ProcessStep(ctx));
            }

            SetAnimSpeed(0f);
            _isMoving = false;
            MoveEnded?.Invoke();
        }

        private IEnumerator ProcessStep(StepContext ctx)
        {
            List<WaypointConnection> path = new List<WaypointConnection>();
            bool encounteredFork = false;
            MapWaypoint tempNode = _currentNode;

            for (int i = 0; i < ctx.StepsRemaining; i++)
            {
                if (tempNode.connections.Count == 0)
                {
                    ctx.StepsRemaining = 0;
                    break;
                }
                if (tempNode.connections.Count == 1)
                {
                    var conn = tempNode.connections[0];
                    path.Add(conn);
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
            SetAnimSpeed(1f);
            foreach (var conn in path)
            {
                yield return StartCoroutine(MoveAlongConnection(conn));
                _currentNode = conn.targetNode;
                ctx.StepsRemaining--;
                yield return StartCoroutine(HandleNodeArrival(_currentNode, ctx.StepsRemaining == 0));
            }
            SetAnimSpeed(0f);
        }

        private IEnumerator HandleFork(StepContext ctx)
        {
            WaypointConnection selectedConnection = null;
            bool selectionResolved = false;

            if (ForkSelectionRequested != null)
            {
                ForkSelectionRequested.Invoke(_currentNode, result =>
                {
                    selectedConnection = result;
                    selectionResolved = true;
                });
                yield return new WaitUntil(() => selectionResolved || !isActiveAndEnabled);
            }
            else
            {
                Debug.LogWarning("[BoardMovementController] ForkSelectionRequested has no listeners.");
                yield break;
            }

            if (!isActiveAndEnabled || selectedConnection == null) yield break;

            yield return new WaitForSeconds(0.2f);
            SetAnimSpeed(1f);
            yield return StartCoroutine(MoveAlongConnection(selectedConnection));
            _currentNode = selectedConnection.targetNode;
            ctx.StepsRemaining--;
            yield return StartCoroutine(HandleNodeArrival(_currentNode, ctx.StepsRemaining == 0));
            SetAnimSpeed(0f);
        }

        // --- 核心修改：支持事件中断的移动逻辑 ---
        private IEnumerator MoveAlongConnection(WaypointConnection conn)
        {
            Vector3 p0 = playerToken.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 curveStartPos = _currentNode.transform.position;
            Vector3 p1 = curveStartPos + conn.controlPointOffset;

            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            float duration = approxDist / moveSpeed;

            int nextEventIndex = 0;
            int totalEvents = conn.events.Count;

            float timer = 0f;

            while (timer < duration)
            {
                if (playerToken == null) yield break;
                float dt = Time.deltaTime;
                float nextTimer = timer + dt;

                float nextT = nextTimer / duration;

                // 检测：这一帧的移动是否“跨越”了下一个事件点
                if (nextEventIndex < totalEvents && conn.events[nextEventIndex].progressPoint <= nextT)
                {
                    ConnectionEvent evt = conn.events[nextEventIndex];

                    float triggerT = evt.progressPoint;
                    Vector3 triggerPos = BezierUtils.GetQuadraticBezierPoint(triggerT, curveStartPos, p1, p2);
                    playerToken.position = triggerPos;
                    timer = triggerT * duration;

                    yield return StartCoroutine(HandleConnectionEvent(evt));

                    // 事件触发完，索引+1，指向下一个
                    nextEventIndex++;
                    continue;
                }

                // 正常移动逻辑
                timer = nextTimer;
                Vector3 nextPos = BezierUtils.GetQuadraticBezierPoint(nextT, curveStartPos, p1, p2);

                if (playerToken == null) yield break;
                Vector3 moveDir = (nextPos - playerToken.position).normalized;
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    playerToken.rotation = Quaternion.Slerp(playerToken.rotation, targetRot, rotateSpeed * dt);
                }

                playerToken.position = nextPos;
                yield return null;
            }
            
            if (playerToken != null) playerToken.position = p2;
        }

        // --- 处理事件的表现 ---
        private IEnumerator HandleConnectionEvent(ConnectionEvent evt)
        {
            // 停止跑步动画
            SetAnimSpeed(0f);

            if (evt.eventAction != null)
            {
                yield return StartCoroutine(evt.eventAction.Execute(BoardGameManager.Instance, evt.contextTarget));
            }
            else
            {
                Debug.LogWarning("Connection Event triggered but no Action SO assigned!");
            }

            // 准备恢复移动
            SetAnimSpeed(1f);
        }

        private IEnumerator HandleNodeArrival(MapWaypoint node, bool isFinalStep)
        {
            if (node == null || node.tileData == null) yield break;
            if (playerToken == null) yield break;

            bool shouldTrigger = isFinalStep || node.tileData.TriggerOnPass;

            if (shouldTrigger) SetAnimSpeed(0f);

            if (shouldTrigger)
            {
                EventBus.Raise(new PlayerReachedNodeEvent { Node = node });
                node.tileData.OnEnter(playerToken.gameObject);
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

            if (!isFinalStep) SetAnimSpeed(1f);
        }

        private void SetAnimSpeed(float value)
        {
            if (_playerAnimator == null) return;
            if (_playerAnimator.runtimeAnimatorController == null) return;
            _playerAnimator.SetFloat(_animIDSpeed, value);
        }

        private bool IsBoardModeActive()
        {
            return GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode;
        }
    }
}
