using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 引用 Input System
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

        // ✅ 新增：Input Action 实例
        private InputSystem_Actions _inputActions;

        protected override void Awake()
        {
            base.Awake();
            // 初始化输入系统实例
            _inputActions = new InputSystem_Actions();
        }

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                ResetToStart();
            }
        }

        // ✅ 必须正确管理 Input 的启用/禁用
        private void OnEnable() 
        {
            GameManager.OnStateChanged += HandleStateChanged;
            _inputActions.Enable();
        }

        private void OnDisable() 
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            _inputActions.Disable();
        }

        private void HandleStateChanged(GameState newState)
        {
            // 当进入决策状态时，我们要确保 InputMap 切换到 UI 或 Player 模式
            // 这里假设默认的 Player Map 包含 Move 和 Interact
        }

        // ... [Roll Dice 和 MoveRoutine 代码保持不变，直到 HandleForkSelection] ...
        [ContextMenu("Roll Dice")]
        public void RollDice()
        {
             if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
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
                // ... [路径查找逻辑与之前相同，省略重复代码以聚焦核心] ...
                // 简单复述：预计算 segmentPath -> 遇到岔路 break
                
                List<WaypointConnection> segmentPath = new List<WaypointConnection>();
                MapWaypoint tempNode = _currentNode;
                bool encounteredFork = false;

                for (int i = 0; i < stepsRemaining; i++)
                {
                    if (tempNode.connections.Count == 0) { stepsRemaining = 0; break; }
                    else if (tempNode.connections.Count == 1) {
                        var conn = tempNode.connections[0];
                        segmentPath.Add(conn);
                        tempNode = conn.targetNode;
                    }
                    else { encounteredFork = true; break; }
                }

                // 移动执行
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

                // 岔路逻辑
                if (encounteredFork && stepsRemaining > 0)
                {
                    GameManager.Instance.ChangeState(GameState.TurnDecision);
                    WaypointConnection selectedConnection = null;
                    yield return StartCoroutine(HandleForkSelection(_currentNode, result => selectedConnection = result));
                    GameManager.Instance.ChangeState(GameState.BoardMode);

                    if (selectedConnection != null)
                    {
                        yield return new WaitForSeconds(0.2f);
                        if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 1f);
                        yield return StartCoroutine(MoveAlongCurve(selectedConnection));
                        _currentNode = selectedConnection.targetNode;
                        stepsRemaining--;
                    }
                    else break;
                }
            }
            
            if (_playerAnimator) _playerAnimator.SetFloat(moveSpeedParam, 0f);
            if (_currentNode.tileData != null) _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            _isMoving = false;
        }

        // ==================== 🕹️ 修正后的 Input System 逻辑 ====================

        private IEnumerator HandleForkSelection(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            List<WaypointConnection> options = forkNode.connections;
            int currentIndex = 0;
            bool selected = false;

            // 生成光标
            ClearCursors();
            for (int i = 0; i < options.Count; i++)
                _spawnedCursors.Add(InstantiateSelectionCursor(options[i]));

            UpdateCursorVisuals(currentIndex);
            
            // 为了防止按一次键触发多次移动，我们需要简单的防抖 (Debounce)
            bool inputReleased = true; 

            while (!selected)
            {
                // ✅ 使用生成的 C# 类读取输入
                // 假设你的 Action Map 叫 "Player"，动作叫 "Move" (Vector2) 和 "Interact" (Button)
                // 如果你的 Map 叫 "UI"，动作叫 "Navigate" 和 "Submit"，请相应修改
                Vector2 moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
                
                // 也有可能你想用专门的 "Interact" 键来确认
                // 如果没有 Interact，可以用 Jump 代替测试
                bool confirmPressed = _inputActions.Player.Interact.IsPressed(); 

                // 方向选择逻辑 (带防抖)
                if (Mathf.Abs(moveInput.x) > 0.5f)
                {
                    if (inputReleased)
                    {
                        if (moveInput.x < 0) currentIndex--;
                        else currentIndex++;

                        // 循环索引
                        if (currentIndex < 0) currentIndex = options.Count - 1;
                        if (currentIndex >= options.Count) currentIndex = 0;

                        UpdateCursorVisuals(currentIndex);
                        inputReleased = false; // 锁定输入，直到归零
                    }
                }
                else
                {
                    inputReleased = true; // 摇杆/按键回正，解锁
                }

                // 确认选择逻辑
                if (confirmPressed) // 建议使用 WasPressedThisFrame() 如果是在 Update 里，但在协程里 IsPressed + 释放锁更安全，或者直接用触发器
                {
                     // 这里为了演示简单，如果你的 Action 是 Button 类型，可以直接用 triggered
                     if (_inputActions.Player.Interact.WasPressedThisFrame())
                     {
                         selected = true;
                     }
                }

                yield return null;
            }

            ClearCursors();
            onSelected?.Invoke(options[currentIndex]);
        }

        // ... [辅助方法 InstantiateSelectionCursor, UpdateCursorVisuals, ClearCursors, MoveAlongCurve, ResetToStart 保持不变] ...
        private GameObject InstantiateSelectionCursor(WaypointConnection conn)
        {
            // (代码略，同上一版)
            GameObject cursor;
            if (cursorPrefab != null) cursor = Instantiate(cursorPrefab);
            else { cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(cursor.GetComponent<Collider>()); }
            Vector3 p0 = _currentNode.transform.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 p1 = p0 + conn.controlPointOffset;
            cursor.transform.position = MapWaypoint.GetBezierPoint(cursorOffsetDistance, p0, p1, p2);
            cursor.transform.localScale = Vector3.one * cursorScale;
            return cursor;
        }

        private void UpdateCursorVisuals(int activeIndex)
        {
            for (int i = 0; i < _spawnedCursors.Count; i++) {
                var r = _spawnedCursors[i].GetComponent<Renderer>();
                if(r) r.material.color = (i == activeIndex) ? Color.green : new Color(1,1,1,0.5f);
            }
        }
        private void ClearCursors() { foreach(var c in _spawnedCursors) if(c) Destroy(c); _spawnedCursors.Clear(); }
        private IEnumerator MoveAlongCurve(WaypointConnection conn) 
        {
            // (代码略，同上一版，贝塞尔移动逻辑)
            Vector3 p0 = playerToken.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 p1 = _currentNode.transform.position + conn.controlPointOffset;
            float duration = (Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2)) / moveSpeed;
            for(float t=0; t<duration; t+=Time.deltaTime) {
                Vector3 pos = MapWaypoint.GetBezierPoint(t/duration, _currentNode.transform.position, p1, p2);
                playerToken.position = pos;
                playerToken.LookAt(2*pos - playerToken.position); // 简易朝向
                yield return null;
            }
            playerToken.position = p2;
        }
        public void ResetToStart()
        {
             StopAllCoroutines(); _isMoving = false; ClearCursors();
             if(startNode && playerToken) { _currentNode = startNode; playerToken.position = startNode.transform.position; }
        }
    }
}