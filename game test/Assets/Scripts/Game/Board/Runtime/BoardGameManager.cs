using System.Collections;
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
        public float moveSpeed = 5f; // 米/秒
        public float rotateSpeed = 10f;

        [Header("Animation")]
        public string speedParam = "Speed"; // Animator 参数名

        // 运行时状态
        private MapWaypoint _currentNode;
        private bool _isMoving = false;
        private Animator _playerAnimator;

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                ResetToStart();
            }
        }

        /// <summary>
        /// 测试功能：重置回起点
        /// </summary>
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
                if (_playerAnimator) _playerAnimator.SetFloat(speedParam, 0);
                
                Debug.Log("Game Reset to Start Node.");
            }
        }

        [ContextMenu("Roll Dice")] 
        public void RollDice()
        {
            if (_isMoving)
            {
                Debug.LogWarning("Player is strictly moving!");
                return;
            }

            int steps = Random.Range(1, 7); 
            Debug.Log($"<color=cyan>掷骰子结果: {steps}</color>");
            
            StartCoroutine(MoveRoutine(steps));
        }

        private IEnumerator MoveRoutine(int steps)
        {
            _isMoving = true;

            for (int i = 0; i < steps; i++)
            {
                // 1. 检查连接
                if (_currentNode.connections.Count == 0)
                {
                    Debug.Log("走到尽头了！");
                    break;
                }

                // 默认走第一条路 (未来可以在这里加 UI 选择分支)
                WaypointConnection connection = _currentNode.connections[0];
                MapWaypoint targetNode = connection.targetNode;

                // 2. 准备曲线数据
                Vector3 p0 = _currentNode.transform.position;
                Vector3 p2 = targetNode.transform.position;
                Vector3 p1 = p0 + connection.controlPointOffset; // 计算控制点

                // 3. 开始移动 (使用匀速估算)
                // 估算曲线长度来决定时间，保持匀速
                float estimatedDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
                float duration = estimatedDist / moveSpeed;
                float timer = 0f;

                // 播放跑动动画
                if (_playerAnimator) _playerAnimator.SetFloat(speedParam, 1f);

                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float t = timer / duration; // 归一化时间 0-1

                    // 获取当前帧目标位置
                    Vector3 nextPos = MapWaypoint.GetBezierPoint(t, p0, p1, p2);
                    
                    // 计算朝向 (看向下一点)
                    Vector3 moveDir = (nextPos - playerToken.position).normalized;
                    if (moveDir != Vector3.zero)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(moveDir);
                        playerToken.rotation = Quaternion.Slerp(playerToken.rotation, targetRot, rotateSpeed * Time.deltaTime);
                    }

                    playerToken.position = nextPos;

                    yield return null;
                }

                // 4. 到达单个格子
                playerToken.position = p2;
                _currentNode = targetNode;
                
                // 短暂停顿
                yield return null;
            }

            // 5. 停止动画
            if (_playerAnimator) _playerAnimator.SetFloat(speedParam, 0f);

            // 6. 触发格子效果
            if (_currentNode.tileData != null)
            {
                _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            }

            _isMoving = false;
        }
    }
}