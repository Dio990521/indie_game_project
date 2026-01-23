using System;
using System.Collections;
using System.Collections.Generic;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardEntity : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private bool isPlayer;
        [SerializeField] private MapWaypoint initialNode;
        [SerializeField] private bool snapToNodeOnStart = true;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotateSpeed = 15f;
        [SerializeField] private Animator animator;
        [SerializeField] private string moveSpeedParamName = "Speed";
        [SerializeField] private bool triggerConnectionEvents = false;

        public bool IsPlayer => isPlayer;
        public MapWaypoint CurrentNode { get; private set; }
        public MapWaypoint CurrentWaypoint => CurrentNode;
        public MapWaypoint LastWaypoint { get; private set; }
        public bool IsMoving { get; private set; }
        public bool TriggerConnectionEvents
        {
            get => triggerConnectionEvents;
            set => triggerConnectionEvents = value;
        }
        public event Action MoveEnded;

        private static readonly List<BoardEntity> Instances = new List<BoardEntity>();
        private int _animIDSpeed;

        private void Awake()
        {
            _animIDSpeed = Animator.StringToHash(moveSpeedParamName);
            CacheAnimator();
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this)) Instances.Add(this);
        }

        private void OnDisable()
        {
            Instances.Remove(this);
        }

        private void Start()
        {
            if (CurrentNode == null && initialNode != null)
            {
                SetCurrentNode(initialNode, snapToNodeOnStart);
            }
        }

        public void SetAsPlayer(bool value)
        {
            isPlayer = value;
        }

        public void SetCurrentNode(MapWaypoint node, bool snapToNode, bool resetLastWaypoint = true)
        {
            CurrentNode = node;
            if (resetLastWaypoint)
            {
                LastWaypoint = null;
            }
            if (snapToNode && node != null)
            {
                transform.position = node.transform.position;
            }
        }

        public void SetMovingState(bool value)
        {
            IsMoving = value;
        }

        public void SetMoveAnimationSpeed(float value)
        {
            if (animator == null) return;
            if (animator.runtimeAnimatorController == null) return;
            animator.SetFloat(_animIDSpeed, value);
        }

        public void MoveTo(int steps)
        {
            if (IsMoving) return;
            if (CurrentNode == null)
            {
                Debug.LogWarning("[BoardEntity] CurrentNode is null, cannot move.");
                return;
            }
            StartCoroutine(MoveRoutine(steps));
        }

        public static BoardEntity FindFirstNpc()
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                if (Instances[i] != null && !Instances[i].isPlayer) return Instances[i];
            }
            return null;
        }

        public static BoardEntity FindOtherAtNode(MapWaypoint node, BoardEntity exclude)
        {
            if (node == null) return null;
            for (int i = 0; i < Instances.Count; i++)
            {
                BoardEntity entity = Instances[i];
                if (entity == null || entity == exclude) continue;
                if (entity.CurrentNode == node) return entity;
            }
            return null;
        }

        private IEnumerator MoveRoutine(int totalSteps)
        {
            SetMovingState(true);
            SetMoveAnimationSpeed(1f);
            int stepsRemaining = totalSteps;
            while (stepsRemaining > 0)
            {
                if (CurrentNode == null || CurrentNode.connections.Count == 0) break;

                WaypointConnection connection = ChooseNextConnection(CurrentNode);
                if (connection == null) break;

                yield return StartCoroutine(MoveAlongConnection(connection));
                stepsRemaining--;
            }
            SetMoveAnimationSpeed(0f);
            SetMovingState(false);
            MoveEnded?.Invoke();
        }

        private WaypointConnection ChooseNextConnection(MapWaypoint node)
        {
            if (node == null || node.connections.Count == 0) return null;
            List<MapWaypoint> validTargets = node.GetValidNextNodes(LastWaypoint);
            if (validTargets.Count == 0) return null;
            if (validTargets.Count == 1) return node.GetConnectionTo(validTargets[0]);

            int index = UnityEngine.Random.Range(0, validTargets.Count);
            return node.GetConnectionTo(validTargets[index]);
        }

        public IEnumerator MoveAlongConnection(WaypointConnection conn)
        {
            if (conn == null || conn.targetNode == null) yield break;
            if (CurrentNode == null)
            {
                Debug.LogWarning("[BoardEntity] CurrentNode is null, cannot move along connection.");
                yield break;
            }

            Vector3 p0 = transform.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 curveStartPos = CurrentNode.transform.position;
            Vector3 p1 = curveStartPos + conn.controlPointOffset;

            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            if (moveSpeed <= 0f || approxDist <= 0f)
            {
                transform.position = p2;
                SetCurrentNode(conn.targetNode, false);
                yield break;
            }
            float duration = approxDist / moveSpeed;

            int nextEventIndex = 0;
            int totalEvents = conn.events.Count;
            float timer = 0f;
            while (timer < duration)
            {
                float dt = Time.deltaTime;
                float nextTimer = timer + dt;
                float nextT = nextTimer / duration;

                if (triggerConnectionEvents && nextEventIndex < totalEvents && conn.events[nextEventIndex].progressPoint <= nextT)
                {
                    ConnectionEvent evt = conn.events[nextEventIndex];

                    float triggerT = evt.progressPoint;
                    Vector3 triggerPos = BezierUtils.GetQuadraticBezierPoint(triggerT, curveStartPos, p1, p2);
                    transform.position = triggerPos;
                    timer = triggerT * duration;

                    yield return StartCoroutine(HandleConnectionEvent(evt));

                    nextEventIndex++;
                    continue;
                }

                timer = nextTimer;
                Vector3 nextPos = BezierUtils.GetQuadraticBezierPoint(nextT, curveStartPos, p1, p2);

                Vector3 moveDir = (nextPos - transform.position).normalized;
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
                }

                transform.position = nextPos;
                yield return null;
            }

            transform.position = p2;
            LastWaypoint = CurrentNode;
            SetCurrentNode(conn.targetNode, false, false);
        }

        private IEnumerator HandleConnectionEvent(ConnectionEvent evt)
        {
            SetMoveAnimationSpeed(0f);

            if (evt.eventAction != null)
            {
                yield return StartCoroutine(evt.eventAction.Execute(BoardGameManager.Instance, evt.contextTarget));
            }
            else
            {
                Debug.LogWarning("Connection Event triggered but no Action SO assigned!");
            }

            SetMoveAnimationSpeed(1f);
        }

        private void CacheAnimator()
        {
            if (animator != null) return;
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i].runtimeAnimatorController == null) continue;
                animator = animators[i];
                return;
            }
        }
    }
}
