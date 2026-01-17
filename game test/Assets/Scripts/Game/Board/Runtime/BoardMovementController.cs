using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Events;
using IndieGame.Gameplay.Board.Data;
using IndieGame.UI.Confirmation;
using System;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardMovementController : MonoBehaviour
    {
        [Header("Dependencies")]
        public BoardForkSelector forkSelector;

        [Header("Game References")]
        public Transform playerToken;
        public MapWaypoint startNode;

        [Header("Settings")]
        public float moveSpeed = 5f;
        public float rotateSpeed = 15f;
        public string moveSpeedParamName = "Speed";

        public bool IsMoving => _isMoving;
        public int CurrentNodeId => _currentNode != null ? _currentNode.nodeID : -1;
        public event Action MoveStarted;
        public event Action MoveEnded;

        private int _animIDSpeed;
        private Animator _playerAnimator;
        private MapWaypoint _currentNode;
        private bool _isMoving = false;

        private void Awake()
        {
            _animIDSpeed = Animator.StringToHash(moveSpeedParamName);
        }

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                if (_currentNode == null)
                {
                    ResetToStart();
                }
            }
            else
            {
                ResolveReferences(GameManager.Instance != null ? GameManager.Instance.LastBoardIndex : -1);
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _isMoving = false;
        }

        public void BeginMove(int totalSteps)
        {
            if (_isMoving) return;
            _isMoving = true;
            MoveStarted?.Invoke();
            StartCoroutine(MoveRoutine(totalSteps));
        }

        public void ResetToStart()
        {
            StopAllCoroutines();
            _isMoving = false;
            if (forkSelector != null) forkSelector.ClearSelection();
            if (startNode && playerToken)
            {
                _currentNode = startNode;
                playerToken.position = startNode.transform.position;
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
            if (playerToken == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerToken = player.transform;
            }

            if (playerToken != null && _playerAnimator == null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
            }

            if (preferredNodeId >= 0)
            {
                SetCurrentNodeById(preferredNodeId);
                return;
            }

            if (startNode == null)
            {
                MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
                if (nodes.Length > 0) startNode = nodes[0];
            }

            if (_currentNode == null && startNode != null)
            {
                _currentNode = startNode;
                if (playerToken != null)
                {
                    playerToken.position = startNode.transform.position;
                }
            }
        }

        private IEnumerator MoveRoutine(int totalSteps)
        {
            int stepsRemaining = totalSteps;

            while (stepsRemaining > 0)
            {
                List<WaypointConnection> segmentPath = new List<WaypointConnection>();
                MapWaypoint tempNode = _currentNode;
                bool encounteredFork = false;

                // 预计算路径（保持原有逻辑）
                for (int i = 0; i < stepsRemaining; i++)
                {
                    if (tempNode.connections.Count == 0) { stepsRemaining = 0; break; }
                    else if (tempNode.connections.Count == 1)
                    {
                        var conn = tempNode.connections[0];
                        segmentPath.Add(conn);
                        tempNode = conn.targetNode;
                    }
                    else { encounteredFork = true; break; }
                }

                // 1. 执行自动移动
                if (segmentPath.Count > 0)
                {
                    if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 1f);
                    foreach (var conn in segmentPath)
                    {
                        // 这里可能会触发“连线事件”，所以移动可能会暂停
                        yield return StartCoroutine(MoveAlongCurve(conn));
                        _currentNode = conn.targetNode;
                        stepsRemaining--;
                        yield return StartCoroutine(HandleNodeArrival(_currentNode, stepsRemaining == 0));
                    }
                    if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 0f);
                }

                // 2. 处理岔路（保持原有逻辑）
                if (encounteredFork && stepsRemaining > 0)
                {
                    GameManager.Instance.ChangeState(GameState.TurnDecision);
                    WaypointConnection selectedConnection = null;
                    if (forkSelector != null)
                    {
                        yield return StartCoroutine(forkSelector.SelectConnection(_currentNode, result => selectedConnection = result));
                    }
                    else
                    {
                        Debug.LogWarning("[BoardMovementController] Fork selection requested but no selector is assigned.");
                    }
                    GameManager.Instance.ChangeState(GameState.BoardMode);

                    if (selectedConnection != null)
                    {
                        yield return new WaitForSeconds(0.2f);
                        if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 1f);
                        yield return StartCoroutine(MoveAlongCurve(selectedConnection));
                        _currentNode = selectedConnection.targetNode;
                        stepsRemaining--;
                        yield return StartCoroutine(HandleNodeArrival(_currentNode, stepsRemaining == 0));
                    }
                    else break;
                }
            }
            
            if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 0f);
            
            _isMoving = false;
            MoveEnded?.Invoke();
        }

        // --- 核心修改：支持事件中断的移动逻辑 ---
        private IEnumerator MoveAlongCurve(WaypointConnection conn)
        {
            if (playerToken == null || _currentNode == null || conn == null || conn.targetNode == null) yield break;
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
            if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 0f);

            if (evt.eventAction != null)
            {
                yield return StartCoroutine(evt.eventAction.Execute(BoardGameManager.Instance, evt.contextTarget));
            }
            else
            {
                Debug.LogWarning("Connection Event triggered but no Action SO assigned!");
            }

            // 准备恢复移动
            if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 1f);
        }

        private IEnumerator HandleNodeArrival(MapWaypoint node, bool isFinalStep)
        {
            if (node == null || node.tileData == null) yield break;
            if (playerToken == null) yield break;

            bool shouldTrigger = isFinalStep || node.tileData.TriggerOnPass;

            if (shouldTrigger && _playerAnimator)
            {
                _playerAnimator.SetFloat(_animIDSpeed, 0f);
            }

            if (shouldTrigger)
            {
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

            if (!isFinalStep && _playerAnimator)
            {
                _playerAnimator.SetFloat(_animIDSpeed, 1f);
            }
        }
    }
}
