using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core; // 引用 GameManager
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
        
        [Header("Selection UI")]
        [Tooltip("岔路选择时的指示物Prefab，如果不填则自动生成黄色球体")]
        public GameObject cursorPrefab; 
        public float cursorOffsetDistance = 0.2f; // 放在连线的 20% 处
        public float cursorScale = 0.5f;

        [Header("Animation")]
        public string moveSpeedParam = "Speed"; 

        // 运行时状态
        private MapWaypoint _currentNode;
        private bool _isMoving = false;
        private Animator _playerAnimator;
        
        // 岔路选择相关
        private List<GameObject> _spawnedCursors = new List<GameObject>();

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                ResetToStart();
            }
        }

        // ------------------ 核心移动循环 (重构版) ------------------

        [ContextMenu("Roll Dice")]
        public void RollDice()
        {
            // 安全检查：只有在 BoardMode 且没在移动时才能掷骰子
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.BoardMode)
            {
                Debug.LogWarning("Current State is not BoardMode.");
                return;
            }
            if (_isMoving) return;

            int steps = Random.Range(1, 7);
            Debug.Log($"<color=cyan>🎲 掷骰子: {steps}</color>");

            StartCoroutine(MoveRoutine(steps));
        }

        private IEnumerator MoveRoutine(int totalSteps)
        {
            _isMoving = true;
            int stepsRemaining = totalSteps;

            // 循环直到步数走完
            while (stepsRemaining > 0)
            {
                // 1. 寻找这一段连续的路径 (直到遇到死路 或 岔路)
                List<WaypointConnection> segmentPath = new List<WaypointConnection>();
                MapWaypoint tempNode = _currentNode;
                bool encounteredFork = false;

                // 预计算能走多远
                for (int i = 0; i < stepsRemaining; i++)
                {
                    if (tempNode.connections.Count == 0)
                    {
                        Debug.Log("前面没路了，强制停止！");
                        stepsRemaining = 0; // 强制结束
                        break;
                    }
                    else if (tempNode.connections.Count == 1)
                    {
                        // 单行道：直接加入路径
                        var conn = tempNode.connections[0];
                        segmentPath.Add(conn);
                        tempNode = conn.targetNode;
                    }
                    else
                    {
                        // 发现岔路 (Count > 1)！
                        // 停止预计算，先走到这个路口再说
                        encounteredFork = true;
                        break; 
                    }
                }

                // 2. 执行这段连续的移动
                if (segmentPath.Count > 0)
                {
                    // 播放动画
                    if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 1f);

                    foreach (var conn in segmentPath)
                    {
                        yield return StartCoroutine(MoveAlongCurve(conn));
                        _currentNode = conn.targetNode;
                        stepsRemaining--; // 每走一格，步数减一
                    }
                    
                    // 暂时停动画（如果在岔路口停下思考的话）
                    if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0f);
                }

                // 3. 处理岔路逻辑
                if (encounteredFork && stepsRemaining > 0)
                {
                    Debug.Log($"<color=yellow>遇到岔路！剩余步数: {stepsRemaining}</color>");
                    
                    // 等待玩家选择路径 -> 返回选择的连接
                    WaypointConnection selectedConnection = null;
                    yield return StartCoroutine(HandleForkSelection(_currentNode, result => selectedConnection = result));

                    if (selectedConnection != null)
                    {
                        // 4. 选中后，立即走这一步离开岔路口
                        // 恢复动画
                        if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 1f);
                        
                        yield return StartCoroutine(MoveAlongCurve(selectedConnection));
                        _currentNode = selectedConnection.targetNode;
                        stepsRemaining--;
                        
                        // 走完这一步后，while循环继续，会再次检测前方是不是又是岔路
                    }
                    else
                    {
                        // 理论上不该发生，除非取消选择逻辑
                        Debug.LogError("未选择路径，中断！");
                        break; 
                    }
                }
            }

            // 5. 移动完全结束
            if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0f);

            Debug.Log($"抵达终点: {_currentNode.nodeID}");
            if (_currentNode.tileData != null)
            {
                _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            }

            _isMoving = false;
        }

        // ------------------ 岔路选择 UI 逻辑 ------------------

        private IEnumerator HandleForkSelection(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            List<WaypointConnection> options = forkNode.connections;
            int currentIndex = 0;
            bool selected = false;

            // 1. 生成选择指示器 (Cursors)
            ClearCursors();
            for (int i = 0; i < options.Count; i++)
            {
                GameObject cursor = InstantiateSelectionCursor(options[i]);
                _spawnedCursors.Add(cursor);
            }

            // 2. 输入循环
            UpdateCursorVisuals(currentIndex);

            // 等待直到玩家按下确认键 (Space / Enter)
            while (!selected)
            {
                // 检测输入 (简单起见使用 Input，如果你完全依赖 InputSystem，这里可以用 playerInput.actions["Navigate"] )
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    currentIndex--;
                    if (currentIndex < 0) currentIndex = options.Count - 1;
                    UpdateCursorVisuals(currentIndex);
                }
                else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    currentIndex++;
                    if (currentIndex >= options.Count) currentIndex = 0;
                    UpdateCursorVisuals(currentIndex);
                }
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    selected = true;
                }

                yield return null;
            }

            // 3. 清理并返回
            ClearCursors();
            onSelected?.Invoke(options[currentIndex]);
        }

        private GameObject InstantiateSelectionCursor(WaypointConnection conn)
        {
            GameObject cursor;
            if (cursorPrefab != null)
            {
                cursor = Instantiate(cursorPrefab);
            }
            else
            {
                // 没有Prefab就创建一个临时的黄色球体
                cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(cursor.GetComponent<Collider>()); // 移除碰撞体防止卡住角色
                cursor.name = "Cursor_Generated";
            }

            // 计算位置：放在贝塞尔曲线的 20% 处，方便看清方向
            Vector3 p0 = _currentNode.transform.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 p1 = p0 + conn.controlPointOffset;
            
            // 放在曲线上 cursorOffsetDistance (0~1) 的位置
            Vector3 pos = MapWaypoint.GetBezierPoint(cursorOffsetDistance, p0, p1, p2);
            
            cursor.transform.position = pos;
            cursor.transform.localScale = Vector3.one * cursorScale;

            return cursor;
        }

        private void UpdateCursorVisuals(int activeIndex)
        {
            for (int i = 0; i < _spawnedCursors.Count; i++)
            {
                var renderer = _spawnedCursors[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 选中的是 绿色，未选中是 白色半透明
                    if (i == activeIndex)
                    {
                        renderer.material.color = Color.green;
                        _spawnedCursors[i].transform.localScale = Vector3.one * (cursorScale * 1.5f); // 选中变大
                    }
                    else
                    {
                        renderer.material.color = new Color(1, 1, 1, 0.5f);
                        _spawnedCursors[i].transform.localScale = Vector3.one * cursorScale;
                    }
                }
            }
        }

        private void ClearCursors()
        {
            foreach (var c in _spawnedCursors)
            {
                if (c != null) Destroy(c);
            }
            _spawnedCursors.Clear();
        }

        // ------------------ 基础移动方法 (保持不变) ------------------

        private IEnumerator MoveAlongCurve(WaypointConnection conn)
        {
            // 确保从当前物体位置开始，避免微小误差跳变
            Vector3 p0 = playerToken.position; 
            Vector3 p2 = conn.targetNode.transform.position;
            // 重新计算控制点（因为起始点可能是动态的，这里简化处理，认为相对 _currentNode 没变）
            Vector3 curveStartPos = _currentNode.transform.position; 
            Vector3 p1 = curveStartPos + conn.controlPointOffset;

            // 估算长度
            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            float duration = approxDist / moveSpeed;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;

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

            playerToken.position = p2;
        }

        public void ResetToStart()
        {
            StopAllCoroutines();
            _isMoving = false;
            ClearCursors(); // 重置时也要清理光标

            if (startNode != null && playerToken != null)
            {
                _currentNode = startNode;
                playerToken.position = startNode.transform.position;
                playerToken.rotation = startNode.transform.rotation;
                if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0);
            }
        }
        
        // 状态切换监听保持不变...
        private void OnEnable() => GameManager.OnStateChanged += HandleStateChanged;
        private void OnDisable() => GameManager.OnStateChanged -= HandleStateChanged;
        private void HandleStateChanged(GameState newState) { /* ... */ }
    }
}