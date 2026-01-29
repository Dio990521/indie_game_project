using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Stats;

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
        [SerializeField] private CharacterStats stats;

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
            if (stats == null) stats = GetComponent<CharacterStats>();
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
                // 初始节点配置时可直接贴到该点
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
                // 切换节点时通常要清理上一个节点引用
                LastWaypoint = null;
            }
            if (snapToNode && node != null)
            {
                // 立即移动到节点位置（用于初始化/传送）
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
            // 由自身执行简化移动逻辑（不含分叉交互）
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
            float speed = stats != null ? stats.MoveSpeed.Value : moveSpeed;
            if (speed <= 0f || approxDist <= 0f)
            {
                // 速度为 0 或距离无效时直接传送到终点
                transform.position = p2;
                SetCurrentNode(conn.targetNode, false);
                yield break;
            }
            float duration = approxDist / speed;

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

                    // 在路径中断点执行事件
                    yield return StartCoroutine(HandleConnectionEvent(evt));

                    nextEventIndex++;
                    continue;
                }

                timer = nextTimer;
                Vector3 nextPos = BezierUtils.GetQuadraticBezierPoint(nextT, curveStartPos, p1, p2);

                Vector3 moveDir = (nextPos - transform.position).normalized;
                if (moveDir != Vector3.zero)
                {
                    // 让角色朝移动方向旋转
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
                // 事件可包含动画/对白等，需要协程等待完成
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
                // 选第一个可用控制器，避免反复查找
                animator = animators[i];
                return;
            }
        }
    }
}
