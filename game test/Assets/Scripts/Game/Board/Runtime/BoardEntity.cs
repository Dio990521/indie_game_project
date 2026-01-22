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

        public bool IsPlayer => isPlayer;
        public MapWaypoint CurrentNode { get; private set; }
        public bool IsMoving { get; private set; }
        public event Action MoveEnded;

        private static readonly List<BoardEntity> Instances = new List<BoardEntity>();

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

        public void SetCurrentNode(MapWaypoint node, bool snapToNode)
        {
            CurrentNode = node;
            if (snapToNode && node != null)
            {
                transform.position = node.transform.position;
            }
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
            IsMoving = true;
            int stepsRemaining = totalSteps;
            while (stepsRemaining > 0)
            {
                if (CurrentNode == null || CurrentNode.connections.Count == 0) break;

                WaypointConnection connection = ChooseNextConnection(CurrentNode);
                if (connection == null) break;

                yield return StartCoroutine(MoveAlongConnection(connection));
                CurrentNode = connection.targetNode;
                stepsRemaining--;
            }
            IsMoving = false;
            MoveEnded?.Invoke();
        }

        private WaypointConnection ChooseNextConnection(MapWaypoint node)
        {
            if (node.connections.Count == 1) return node.connections[0];
            int index = UnityEngine.Random.Range(0, node.connections.Count);
            return node.connections[index];
        }

        private IEnumerator MoveAlongConnection(WaypointConnection conn)
        {
            Vector3 p0 = transform.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 curveStartPos = CurrentNode.transform.position;
            Vector3 p1 = curveStartPos + conn.controlPointOffset;

            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            float duration = approxDist / moveSpeed;

            float timer = 0f;
            while (timer < duration)
            {
                float dt = Time.deltaTime;
                timer += dt;
                float t = timer / duration;
                Vector3 nextPos = BezierUtils.GetQuadraticBezierPoint(t, curveStartPos, p1, p2);

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
        }
    }
}
