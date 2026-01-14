using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // 需要引用 Linq 进行排序
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
            if (inputReader != null) inputReader.InteractEvent += OnInteractInput;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            if (inputReader != null) inputReader.InteractEvent -= OnInteractInput;
        }

        private void OnInteractInput() => _interactTriggered = true;
        private void HandleStateChanged(GameState newState) { }

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

                // 预计算路径（保持原有逻辑）
                for (int i = 0; i < stepsRemaining; i++)
                {
                    if (tempNode.connections.Count == 0) { stepsRemaining = 0; break; }
                    else if (tempNode.connections.Count == 1) 
                    {
                        var conn = tempNode.connections[0];
                        segmentPath.Add(conn);
                        tempNode = conn.targetNode;
                    }
                    else { encounteredFork = true; break; }
                }

                // 1. 执行自动移动
                if (segmentPath.Count > 0)
                {
                    if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 1f);
                    foreach (var conn in segmentPath)
                    {
                        // 这里可能会触发“连线事件”，所以移动可能会暂停
                        yield return StartCoroutine(MoveAlongCurve(conn));
                        _currentNode = conn.targetNode;
                        stepsRemaining--;
                    }
                    if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 0f);
                }

                // 2. 处理岔路（保持原有逻辑）
                if (encounteredFork && stepsRemaining > 0)
                {
                    GameManager.Instance.ChangeState(GameState.TurnDecision);
                    WaypointConnection selectedConnection = null;
                    yield return StartCoroutine(HandleForkSelection(_currentNode, result => selectedConnection = result));
                    GameManager.Instance.ChangeState(GameState.BoardMode);

                    if (selectedConnection != null)
                    {
                        yield return new WaitForSeconds(0.2f);
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

        // --- 核心修改：支持事件中断的移动逻辑 ---
        private IEnumerator MoveAlongCurve(WaypointConnection conn)
        {
            Vector3 p0 = playerToken.position; 
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 curveStartPos = _currentNode.transform.position; 
            Vector3 p1 = curveStartPos + conn.controlPointOffset;

            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            float duration = approxDist / moveSpeed;
            
            // 准备事件队列：按进度从小到大排序，重置触发状态
            foreach(var evt in conn.events) evt.hasTriggered = false;
            Queue<ConnectionEvent> eventQueue = new Queue<ConnectionEvent>(
                conn.events.OrderBy(e => e.progressPoint)
            );

            float timer = 0f;

            while (timer < duration)
            {
                float dt = Time.deltaTime;
                float nextTimer = timer + dt;
                
                float currentT = timer / duration;
                float nextT = nextTimer / duration;

                // 检测：这一帧的移动是否“跨越”了下一个事件点
                if (eventQueue.Count > 0 && eventQueue.Peek().progressPoint <= nextT)
                {
                    // 取出事件
                    ConnectionEvent evt = eventQueue.Dequeue();
                    
                    // 1. 强制移动到精确的触发点位置（防止一帧跳过）
                    float triggerT = evt.progressPoint;
                    Vector3 triggerPos = MapWaypoint.GetBezierPoint(triggerT, curveStartPos, p1, p2);
                    playerToken.position = triggerPos;
                    
                    // 同步时间变量
                    timer = triggerT * duration;

                    // 2. 暂停移动，执行事件逻辑
                    yield return StartCoroutine(HandleConnectionEvent(evt));

                    // 3. 事件结束后，本帧结束，下一帧继续移动
                    // 注意：这里不增加 timer，相当于在这一帧停住了
                    continue; 
                }

                // 正常移动逻辑
                timer = nextTimer;
                Vector3 nextPos = MapWaypoint.GetBezierPoint(nextT, curveStartPos, p1, p2);
                
                Vector3 moveDir = (nextPos - playerToken.position).normalized;
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    playerToken.rotation = Quaternion.Slerp(playerToken.rotation, targetRot, rotateSpeed * dt);
                }

                playerToken.position = nextPos;
                yield return null;
            }
            
            playerToken.position = p2;
        }

        // --- 新增：处理事件的表现 ---
        private IEnumerator HandleConnectionEvent(ConnectionEvent evt)
        {
            // 停止跑步动画
            if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 0f);

            // 1. 转向看向目标点
            if (evt.lookAtTarget != null)
            {
                Quaternion targetRot = Quaternion.LookRotation(evt.lookAtTarget.position - playerToken.position);
                float rotateTimer = 0f;
                while (rotateTimer < 0.5f) // 0.5秒转过去
                {
                    rotateTimer += Time.deltaTime;
                    playerToken.rotation = Quaternion.Slerp(playerToken.rotation, targetRot, rotateTimer * 5f);
                    yield return null;
                }
            }

            // 2. 触发逻辑 (Log)
            Debug.Log($"<color=yellow>⚡ [Path Event] Triggered: {evt.eventMessage}</color>");
            
            // 3. 模拟事件持续时间（比如播放一个特效，或者等待对话框关闭）
            // 未来这里可以改成 yield return DialogueManager.Show(evt.message);
            yield return new WaitForSeconds(1.0f);

            // 4. 准备恢复移动
            // 如果需要，可以加一点延迟再起跑
            if (_playerAnimator) _playerAnimator.SetFloat(_animIDSpeed, 1f);
        }

        private IEnumerator HandleForkSelection(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            List<WaypointConnection> options = forkNode.connections;
            int currentIndex = 0;
            bool selected = false;
            
            _interactTriggered = false; 

            viewHelper.ShowCursors(options, forkNode.transform.position);
            viewHelper.HighlightCursor(currentIndex);
            
            float inputDelay = 0.2f;
            float nextInputTime = 0f;

            yield return null; 

            while (!selected)
            {
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

                if (_interactTriggered)
                {
                    selected = true;
                    _interactTriggered = false;
                }

                yield return null;
            }

            viewHelper.ClearCursors();
            onSelected?.Invoke(options[currentIndex]);
        }

        public void ResetToStart()
        {
             StopAllCoroutines(); 
             _isMoving = false; 
             if(viewHelper) viewHelper.ClearCursors();
             if(startNode && playerToken) 
             { 
                 _currentNode = startNode; 
                 playerToken.position = startNode.transform.position; 
             }
        }
    }
}