using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Data;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("References")]
        public Transform playerToken;
        public MapWaypoint startNode;

        [Header("Settings")]
        public float moveSpeed = 5f;
        public float rotateSpeed = 15f;

        [Header("Animation")]
        public string moveSpeedParam = "Speed"; // 确保此参数在 Animator 中存在 (Float)

        private MapWaypoint _currentNode;
        private bool _isMoving = false;
        private Animator _playerAnimator;

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                if (_playerAnimator == null) 
                    Debug.LogWarning("Player Animator not found in children!");

                ResetToStart();
            }
        }

        public void ResetToStart()
        {
            StopAllCoroutines();
            _isMoving = false;

            if (startNode != null && playerToken != null)
            {
                _currentNode = startNode;
                playerToken.position = startNode.transform.position;
                playerToken.rotation = startNode.transform.rotation;
                
                // 重置动画
                if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0);
            }
        }

        [ContextMenu("Roll Dice")]
        public void RollDice()
        {
            if (_isMoving) return;

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");

            StartCoroutine(MoveRoutine(steps));
        }

        private IEnumerator MoveRoutine(int steps)
        {
            _isMoving = true;

            // 1. 预计算路径 (Pre-calculate Path)
            // 这样我们可以在遇到岔路时提前停下，或者一次性拿完所有路径数据
            List<WaypointConnection> pathQueue = new List<WaypointConnection>();
            MapWaypoint tempNode = _currentNode;

            for (int i = 0; i < steps; i++)
            {
                if (tempNode.connections.Count == 0) break; // 无路可走

                // 处理岔路 (Fork Logic)
                // 如果有多个连接，暂时默认走第一个 (未来这里可以弹出 UI 让玩家选)
                // 即使是 "自动连接"，如果 connections.Count > 1，就说明有分叉
                int pathIndex = 0; 
                if (tempNode.connections.Count > 1)
                {
                    // TODO: 这里可以加入随机选择或者等待玩家输入
                    // pathIndex = Random.Range(0, tempNode.connections.Count);
                    Debug.Log($"遇到岔路在节点 {tempNode.nodeID}，默认走路径 {pathIndex}");
                }

                WaypointConnection nextConn = tempNode.connections[pathIndex];
                pathQueue.Add(nextConn);
                tempNode = nextConn.targetNode;
            }

            // 2. 开始连续移动 (Continuous Movement)
            if (pathQueue.Count > 0)
            {
                // 播放移动动画
                if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 1f);

                foreach (var conn in pathQueue)
                {
                    yield return StartCoroutine(MoveAlongCurve(conn));
                    // 这里不加 yield return new WaitForSeconds，保证无缝衔接
                    
                    // 更新逻辑上的当前节点
                    _currentNode = conn.targetNode;
                }

                // 停止动画
                if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0f);
            }

            // 3. 触发终点效果
            Debug.Log($"抵达终点节点: {_currentNode.nodeID}");
            if (_currentNode.tileData != null)
            {
                _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            }

            _isMoving = false;
        }

        private IEnumerator MoveAlongCurve(WaypointConnection conn)
        {
            Vector3 p0 = playerToken.position; // 总是从当前实际位置出发，保证平滑
            Vector3 p2 = conn.targetNode.transform.position;
            // 因为 p0 变了，我们需要根据 conn 数据反推世界坐标控制点
            // 注意：这里最好用该段连接原本的起始点来计算控制点，或者简单起见直接用 conn 的设定
            // 修正：conn 是属于 _currentNode 的，所以 transform 是 _currentNode
            Vector3 curveStartPos = _currentNode.transform.position; 
            Vector3 p1 = curveStartPos + conn.controlPointOffset;

            // 估算曲线长度来保持匀速
            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            float duration = approxDist / moveSpeed;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

                // 贝塞尔插值
                Vector3 nextPos = MapWaypoint.GetBezierPoint(t, curveStartPos, p1, p2);

                // 旋转朝向
                Vector3 moveDir = (nextPos - playerToken.position).normalized;
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    playerToken.rotation = Quaternion.Slerp(playerToken.rotation, targetRot, rotateSpeed * Time.deltaTime);
                }

                playerToken.position = nextPos;
                yield return null;
            }

            // 强制归位，消除误差
            playerToken.position = p2;
        }
    }
}