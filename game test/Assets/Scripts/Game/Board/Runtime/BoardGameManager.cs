using System.Collections;
using System.Collections.Generic; // 确保引用 List
using UnityEngine;
using IndieGame.Core; // 引用 Core
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
        public string moveSpeedParam = "Speed";

        private MapWaypoint _currentNode;
        private bool _isMoving = false;
        private Animator _playerAnimator;

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                // 初始化时，尝试找到离玩家最近的节点作为 _currentNode
                // 在正式游戏里，这应该由存档加载决定
                _currentNode = startNode; 
            }
        }

        // ==================== 状态管理核心代码 ====================
        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState newState)
        {
            // 这里可以做一些 UI 的显示/隐藏逻辑
            if (newState == GameState.BoardMode)
            {
                // 如果需要，这里可以强制把玩家拉回到最近的格子位置
                // SnapPlayerToNode(); 
            }
        }
        // ========================================================

        // 修改 RollDice，加入状态检查
        [ContextMenu("Roll Dice")]
        public void RollDice()
        {
            // 1. 检查状态
            if (GameManager.Instance.CurrentState != GameState.BoardMode)
            {
                Debug.LogWarning("无法掷骰子：当前不是棋盘模式！(请按 F1 切换)");
                return;
            }

            if (_isMoving) return;

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");

            StartCoroutine(MoveRoutine(steps));
        }

        // ... MoveRoutine, MoveAlongCurve, ResetToStart 保持不变 ...
        // (请保留上一轮你已经写好的这些逻辑，记得把 MoveRoutine, MoveAlongCurve, ResetToStart 完整放进去)
        
        // 为了完整性，我把之前的关键协程逻辑再次列出（缩略版）：
        private IEnumerator MoveRoutine(int steps)
        {
            _isMoving = true;
            // ... (这里的路径查找逻辑保持不变) ...
            List<WaypointConnection> pathQueue = new List<WaypointConnection>();
            MapWaypoint tempNode = _currentNode;

            for (int i = 0; i < steps; i++)
            {
                if (tempNode.connections.Count == 0) break; 
                int pathIndex = 0; 
                // 简单处理岔路
                WaypointConnection nextConn = tempNode.connections[pathIndex];
                pathQueue.Add(nextConn);
                tempNode = nextConn.targetNode;
            }

            if (pathQueue.Count > 0)
            {
                if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 1f);
                foreach (var conn in pathQueue)
                {
                    yield return StartCoroutine(MoveAlongCurve(conn));
                    _currentNode = conn.targetNode;
                }
                if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0f);
            }

            if (_currentNode.tileData != null)
            {
                _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            }
            _isMoving = false;
        }

        private IEnumerator MoveAlongCurve(WaypointConnection conn)
        {
            Vector3 p0 = playerToken.position; 
            Vector3 p2 = conn.targetNode.transform.position;
            // 获取贝塞尔控制点
            Vector3 curveStartPos = _currentNode.transform.position; 
            Vector3 p1 = curveStartPos + conn.controlPointOffset;

            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            float duration = approxDist / moveSpeed;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                Vector3 nextPos = MapWaypoint.GetBezierPoint(t, curveStartPos, p1, p2);
                
                Vector3 moveDir = (nextPos - playerToken.position).normalized;
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    playerToken.rotation = Quaternion.Slerp(playerToken.rotation, targetRot, rotateSpeed * Time.deltaTime);
                }

                playerToken.position = nextPos;
                yield return null;
            }
            playerToken.position = p2;
        }
        
        public void ResetToStart()
        {
             // ... 保留之前的重置逻辑 ...
             StopAllCoroutines();
             _isMoving = false;
             if (startNode != null && playerToken != null)
             {
                 _currentNode = startNode;
                 playerToken.position = startNode.transform.position;
                 playerToken.rotation = startNode.transform.rotation;
                 if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0);
             }
        }
    }
}