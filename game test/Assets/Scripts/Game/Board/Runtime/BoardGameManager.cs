using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Core.Input; 
using IndieGame.Gameplay.Board.View; 

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardGameManager : MonoSingleton<BoardGameManager>
    {
        [Header("Architecture Dependencies")]
        public GameInputReader inputReader;
        public BoardViewHelper viewHelper;

        [Header("Game References")]
        public Transform playerToken;
        public MapWaypoint startNode;

        [Header("Settings")]
        public float moveSpeed = 5f;
        public float rotateSpeed = 15f;
        public string moveSpeedParamName = "Speed"; 

        private int _animIDSpeed;
        private Animator _playerAnimator;
        private MapWaypoint _currentNode;
        private bool _isMoving = false;
        
        // 输入信号缓存
        private bool _interactTriggered = false;

        protected override void Awake()
        {
            base.Awake();
            _animIDSpeed = Animator.StringToHash(moveSpeedParamName);
        }

        private void Start()
        {
            if (playerToken != null)
            {
                _playerAnimator = playerToken.GetComponentInChildren<Animator>();
                ResetToStart();
            }
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            if (inputReader != null)
            {
                inputReader.InteractEvent += OnInteractInput;
            }
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            if (inputReader != null)
            {
                inputReader.InteractEvent -= OnInteractInput;
            }
        }

        // --- 核心修复 ---
        private void OnInteractInput()
        {
            // 移除状态判断。只要按了，就记录。
            // 具体的“是否应该响应”由读取这个布尔值的逻辑决定。
            _interactTriggered = true;
            
            // Debug.Log("Interact Pressed!"); // 用于调试确认按键是否生效
        }
        // ----------------

        private void HandleStateChanged(GameState newState)
        {
        }

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
                List<WaypointConnection> segmentPath = new List<WaypointConnection>();
                MapWaypoint tempNode = _currentNode;
                bool encounteredFork = false;

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

                if (segmentPath.Count > 0)
                {
                    if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 1f);
                    foreach (var conn in segmentPath)
                    {
                        yield return StartCoroutine(MoveAlongCurve(conn));
                        _currentNode = conn.targetNode;
                        stepsRemaining--;
                    }
                    if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 0f);
                }

                if (encounteredFork && stepsRemaining > 0)
                {
                    GameManager.Instance.ChangeState(GameState.TurnDecision);
                    
                    WaypointConnection selectedConnection = null;
                    yield return StartCoroutine(HandleForkSelection(_currentNode, result => selectedConnection = result));
                    
                    GameManager.Instance.ChangeState(GameState.BoardMode);

                    if (selectedConnection != null)
                    {
                        yield return new WaitForSeconds(0.2f); // 小停顿让镜头感更好
                        if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 1f);
                        yield return StartCoroutine(MoveAlongCurve(selectedConnection));
                        _currentNode = selectedConnection.targetNode;
                        stepsRemaining--;
                    }
                    else break;
                }
            }
            
            if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 0f);
            if (_currentNode.tileData != null) _currentNode.tileData.OnPlayerStop(playerToken.gameObject);
            
            _isMoving = false;
        }

        private IEnumerator HandleForkSelection(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            List<WaypointConnection> options = forkNode.connections;
            int currentIndex = 0;
            bool selected = false;
            
            // --- 核心修复 ---
            // 协程开始时清空之前的任何按键缓存
            // 确保这里只响应玩家看到 UI 后的操作
            _interactTriggered = false; 

            viewHelper.ShowCursors(options, forkNode.transform.position);
            viewHelper.HighlightCursor(currentIndex);
            
            float inputDelay = 0.2f;
            float nextInputTime = 0f;

            // 等待一帧，防止同一个按键事件在极短时间内穿透
            yield return null; 

            while (!selected)
            {
                // 处理方向选择
                Vector2 moveInput = inputReader.CurrentMoveInput;
                if (Time.time > nextInputTime && Mathf.Abs(moveInput.x) > 0.5f)
                {
                    if (moveInput.x < 0) currentIndex--;
                    else currentIndex++;

                    if (currentIndex < 0) currentIndex = options.Count - 1;
                    if (currentIndex >= options.Count) currentIndex = 0;

                    viewHelper.HighlightCursor(currentIndex);
                    nextInputTime = Time.time + inputDelay;
                }

                // 处理确认
                if (_interactTriggered)
                {
                    selected = true;
                    _interactTriggered = false; // 消费掉这个输入
                }

                yield return null;
            }

            viewHelper.ClearCursors();
            onSelected?.Invoke(options[currentIndex]);
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
             viewHelper.ClearCursors();
             if(startNode && playerToken) 
             { 
                 _currentNode = startNode; 
                 playerToken.position = startNode.transform.position; 
             }
        }
    }
}