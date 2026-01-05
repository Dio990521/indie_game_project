using System.Collections;
using UnityEngine;
using IndieGame.Core.Utilities; // 引用你的单例模板
using IndieGame.Gameplay.Board.Data;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("References")]
        public Transform playerToken; // 玩家的棋子模型
        public MapWaypoint startNode; // 起始点

        [Header("Settings")]
        public float moveSpeed = 5f;
        public float jumpHeight = 0.5f; // 类似马里奥跳格子的效果

        // 运行时状态
        private MapWaypoint _currentNode;
        private bool _isMoving = false;

        private void Start()
        {
            if (startNode != null && playerToken != null)
            {
                _currentNode = startNode;
                // 初始化位置，修正Y轴
                playerToken.position = startNode.transform.position; 
            }
        }

        /// <summary>
        /// 核心方法：掷骰子并行动
        /// </summary>
        [ContextMenu("Roll Dice and Move")] // 允许在编辑器组件菜单右键调用
        public void RollDice()
        {
            if (_isMoving)
            {
                Debug.LogWarning("Player is strictly moving!");
                return;
            }

            int steps = Random.Range(1, 7); // 1 到 6
            Debug.Log($"<color=cyan>掷骰子结果: {steps}</color>");
            
            StartCoroutine(MoveRoutine(steps));
        }

        private IEnumerator MoveRoutine(int steps)
        {
            _isMoving = true;

            for (int i = 0; i < steps; i++)
            {
                // 1. 检查是否有路可走
                if (_currentNode.nextWaypoints.Count == 0)
                {
                    Debug.Log("走到尽头了！");
                    break;
                }

                // 简单处理：如果有岔路，默认走第一条 (未来这里可以弹出UI让玩家选路)
                MapWaypoint targetNode = _currentNode.nextWaypoints[0];

                // 2. 移动动画 (抛物线跳跃)
                Vector3 startPos = playerToken.position;
                Vector3 endPos = targetNode.transform.position;
                float progress = 0f;

                while (progress < 1f)
                {
                    progress += Time.deltaTime * moveSpeed;
                    
                    // 线性插值位置
                    Vector3 currentPos = Vector3.Lerp(startPos, endPos, progress);
                    
                    // 加一点跳跃高度 (Sine曲线)
                    currentPos.y += Mathf.Sin(progress * Mathf.PI) * jumpHeight;

                    playerToken.position = currentPos;
                    
                    // 面向目标
                    playerToken.LookAt(new Vector3(endPos.x, playerToken.position.y, endPos.z));

                    yield return null;
                }

                // 3. 到达该格
                playerToken.position = endPos;
                _currentNode = targetNode;
                
                // 停顿一小下，更有节奏感
                yield return new WaitForSeconds(0.1f);
            }

            // 4. 移动结束，触发格子效果
            if (_currentNode.tileData != null)
            {
                _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            }
            else
            {
                Debug.LogWarning($"格子 {_currentNode.name} 丢失了 TileData!");
            }

            _isMoving = false;
        }
    }
}