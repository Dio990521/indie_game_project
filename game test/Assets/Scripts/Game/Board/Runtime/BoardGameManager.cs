using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 1. 引入 Input System 命名空间
using IndieGame.Core;
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
        public GameObject cursorPrefab; 
        public float cursorOffsetDistance = 0.2f; 
        public float cursorScale = 0.5f;

        [Header("Animation")]
        public string moveSpeedParam = "Speed"; 

        private MapWaypoint _currentNode;
        private bool _isMoving = false;
        private Animator _playerAnimator;
        private List<GameObject> _spawnedCursors = new List<GameObject>();

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                ResetToStart();
            }
        }

        // ==================== 🎮 状态监听 ====================
        private void OnEnable() => GameManager.OnStateChanged += HandleStateChanged;
        private void OnDisable() => GameManager.OnStateChanged -= HandleStateChanged;

        private void HandleStateChanged(GameState newState)
        {
            // 如果切回 BoardMode，且之前在选路状态，这里可以做恢复逻辑（Demo 暂时不需要）
        }

        // ==================== 🎲 核心流程 ====================

        [ContextMenu("Roll Dice")]
        public void RollDice()
        {
            // 只有在 BoardMode 才能掷骰子 (TurnDecision 状态下不能掷骰子)
            if (GameManager.Instance.CurrentState != GameState.BoardMode)
            {
                Debug.LogWarning($"当前状态 {GameManager.Instance.CurrentState} 不允许掷骰子");
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

            while (stepsRemaining > 0)
            {
                List<WaypointConnection> segmentPath = new List<WaypointConnection>();
                MapWaypoint tempNode = _currentNode;
                bool encounteredFork = false;

                // 1. 预计算路径
                for (int i = 0; i < stepsRemaining; i++)
                {
                    if (tempNode.connections.Count == 0)
                    {
                        stepsRemaining = 0; 
                        break;
                    }
                    else if (tempNode.connections.Count == 1)
                    {
                        var conn = tempNode.connections[0];
                        segmentPath.Add(conn);
                        tempNode = conn.targetNode;
                    }
                    else
                    {
                        encounteredFork = true;
                        break; 
                    }
                }

                // 2. 执行自动移动
                if (segmentPath.Count > 0)
                {
                    if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 1f);
                    foreach (var conn in segmentPath)
                    {
                        yield return StartCoroutine(MoveAlongCurve(conn));
                        _currentNode = conn.targetNode;
                        stepsRemaining--;
                    }
                    if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0f);
                }

                // 3. 处理岔路
                if (encounteredFork && stepsRemaining > 0)
                {
                    Debug.Log($"<color=yellow>遇到岔路，切换状态至 [TurnDecision]...</color>");
                    
                    // A. 切换到决策状态
                    // 这会通知其他系统（如UI层显示提示，SimpleMover保持禁用）
                    GameManager.Instance.ChangeState(GameState.TurnDecision);

                    // B. 等待玩家选择
                    WaypointConnection selectedConnection = null;
                    yield return StartCoroutine(HandleForkSelection(_currentNode, result => selectedConnection = result));

                    // C. 选择完毕，切回 BoardMode 继续跑
                    GameManager.Instance.ChangeState(GameState.BoardMode);

                    if (selectedConnection != null)
                    {
                        // 稍微给一点延迟让状态切换平滑
                        yield return new WaitForSeconds(0.2f);
                        
                        if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 1f);
                        yield return StartCoroutine(MoveAlongCurve(selectedConnection));
                        _currentNode = selectedConnection.targetNode;
                        stepsRemaining--;
                    }
                    else
                    {
                        break; 
                    }
                }
            }

            if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0f);
            
            // 触发格子逻辑
            if (_currentNode.tileData != null)
            {
                _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            }

            _isMoving = false;
        }

        // ==================== 🕹️ 岔路选择逻辑 (Input System 版) ====================

        private IEnumerator HandleForkSelection(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            List<WaypointConnection> options = forkNode.connections;
            int currentIndex = 0;
            bool selected = false;

            ClearCursors();
            for (int i = 0; i < options.Count; i++)
            {
                GameObject cursor = InstantiateSelectionCursor(options[i]);
                _spawnedCursors.Add(cursor);
            }

            UpdateCursorVisuals(currentIndex);

            // 输入检测循环
            // 注意：为了保证输入响应灵敏，我们每帧检测
            // 这里使用 Keyboard.current 等直接访问硬件 API，这是最快修好 Bug 的方式
            // 在更完善的 UI 系统中，你应该监听 UI Action Map 的 Navigate 事件
            while (!selected)
            {
                bool leftPressed = false;
                bool rightPressed = false;
                bool confirmPressed = false;

                // 检测键盘
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) leftPressed = true;
                    if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) rightPressed = true;
                    if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) confirmPressed = true;
                }

                // 检测手柄 (Gamepad) - 可选
                if (Gamepad.current != null)
                {
                    if (Gamepad.current.dpad.left.wasPressedThisFrame) leftPressed = true;
                    if (Gamepad.current.dpad.right.wasPressedThisFrame) rightPressed = true;
                    if (Gamepad.current.buttonSouth.wasPressedThisFrame) confirmPressed = true; // A键 / X键
                }

                if (leftPressed)
                {
                    currentIndex--;
                    if (currentIndex < 0) currentIndex = options.Count - 1;
                    UpdateCursorVisuals(currentIndex);
                }
                else if (rightPressed)
                {
                    currentIndex++;
                    if (currentIndex >= options.Count) currentIndex = 0;
                    UpdateCursorVisuals(currentIndex);
                }
                else if (confirmPressed)
                {
                    selected = true;
                }

                yield return null;
            }

            ClearCursors();
            onSelected?.Invoke(options[currentIndex]);
        }

        private GameObject InstantiateSelectionCursor(WaypointConnection conn)
        {
            GameObject cursor;
            if (cursorPrefab != null) cursor = Instantiate(cursorPrefab);
            else {
                cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(cursor.GetComponent<Collider>());
            }

            Vector3 p0 = _currentNode.transform.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 p1 = p0 + conn.controlPointOffset;
            
            // 放在曲线 20% 处
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
                    if (i == activeIndex)
                    {
                        renderer.material.color = Color.green;
                        _spawnedCursors[i].transform.localScale = Vector3.one * (cursorScale * 1.5f);
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
            foreach (var c in _spawnedCursors) if (c != null) Destroy(c);
            _spawnedCursors.Clear();
        }

        private IEnumerator MoveAlongCurve(WaypointConnection conn)
        {
            Vector3 p0 = playerToken.position; 
            Vector3 p2 = conn.targetNode.transform.position;
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
            StopAllCoroutines();
            _isMoving = false;
            ClearCursors();
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