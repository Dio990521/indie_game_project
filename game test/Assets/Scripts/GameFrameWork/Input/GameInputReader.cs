using System;
using UnityEngine;
using UnityEngine.InputSystem;
using IndieGame.Core;

namespace IndieGame.Core.Input
{
    /// <summary>
    /// 输入读取器（ScriptableObject）：
    /// 充当 Input System 与游戏逻辑之间的中间层。
    /// 统一通过 EventBus 分发输入事件，避免模块直接耦合。
    /// </summary>
    [CreateAssetMenu(fileName = "GameInputReader", menuName = "IndieGame/Core/Input Reader")]
    public class GameInputReader : ScriptableObject, InputSystem_Actions.IPlayerActions, InputSystem_Actions.IUIActions
    {
        /// <summary>
        /// 输入模式：
        /// Gameplay：游戏输入有效
        /// UIOnly：仅 UI 生效，游戏输入关闭
        /// Disabled：完全禁用
        /// </summary>
        public enum InputMode
        {
            Gameplay,
            UIOnly,
            Disabled
        }

        // 缓存当前的移动输入，方便非事件驱动的逻辑（如每帧检测）读取
        public Vector2 CurrentMoveInput { get; private set; }

        /// <summary>
        /// UI 取消事件（通常由 ESC / 手柄 B / Cancel 输入触发）：
        /// UI 控制器（如打造界面）应通过订阅/注销该事件来实现“安全关闭”。
        /// </summary>
        public event Action UICancelEvent;

        private InputSystem_Actions _gameInput;
        private InputMode _currentMode = InputMode.Gameplay;
        private int _inputLockCount;
        private InputMode _modeBeforeLock = InputMode.Gameplay;

        private void OnEnable()
        {
            if (_gameInput == null)
            {
                _gameInput = new InputSystem_Actions();
                
                // 注册回调接口到 InputSystem：
                // - Player Map 负责游戏内输入（移动、交互等）
                // - UI Map 负责通用 UI 输入（特别是 Cancel）
                _gameInput.Player.SetCallbacks(this);
                _gameInput.UI.SetCallbacks(this);
            }
            
            // 默认开启输入
            EnableGameplayInput();
            EventBus.Subscribe<InputLockRequestedEvent>(HandleInputLockRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InputLockRequestedEvent>(HandleInputLockRequested);
            DisableAllInput();
        }

        /// <summary>
        /// 开启 Gameplay 输入。
        /// </summary>
        public void EnableGameplayInput()
        {
            // Gameplay 模式下同时启用 UI Map，确保 ESC/Cancel 在任意 UI 中都能被捕获。
            SetInputMode(InputMode.Gameplay);
        }

        /// <summary>
        /// 禁用所有输入。
        /// </summary>
        public void DisableAllInput()
        {
            if (_gameInput == null) return;
            _gameInput.Player.Disable();
            _gameInput.UI.Disable();
        }

        /// <summary>
        /// 设置输入模式：
        /// 会根据模式启用/禁用 Player 输入，并在必要时清零移动输入。
        /// </summary>
        public void SetInputMode(InputMode mode)
        {
            _currentMode = mode;
            switch (mode)
            {
                case InputMode.Gameplay:
                    // Gameplay：允许玩家输入，同时保留 UI Cancel（ESC）监听能力
                    _gameInput.Player.Enable();
                    _gameInput.UI.Enable();
                    break;
                case InputMode.UIOnly:
                    // UIOnly：禁用玩家输入，但保留 UI 输入（用于菜单导航/取消）
                    _gameInput.Player.Disable();
                    _gameInput.UI.Enable();
                    CurrentMoveInput = Vector2.zero;
                    EventBus.Raise(new InputMoveEvent { Value = Vector2.zero });
                    break;
                case InputMode.Disabled:
                    // Disabled：完全禁用输入（Player + UI）
                    _gameInput.Player.Disable();
                    _gameInput.UI.Disable();
                    CurrentMoveInput = Vector2.zero;
                    EventBus.Raise(new InputMoveEvent { Value = Vector2.zero });
                    break;
            }
        }

        private void HandleInputLockRequested(InputLockRequestedEvent evt)
        {
            if (evt.Locked)
            {
                // 允许嵌套锁（多处同时触发加载/遮罩）
                if (_inputLockCount == 0)
                {
                    _modeBeforeLock = _currentMode;
                    SetInputMode(InputMode.Disabled);
                }
                _inputLockCount++;
                return;
            }

            if (_inputLockCount <= 0) return;
            _inputLockCount--;
            if (_inputLockCount == 0)
            {
                SetInputMode(_modeBeforeLock);
            }
        }

        #region InputSystem Callbacks

        public void OnMove(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            CurrentMoveInput = context.ReadValue<Vector2>();
            // EventBus 分发（新逻辑）
            EventBus.Raise(new InputMoveEvent { Value = CurrentMoveInput });
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            if (context.phase == InputActionPhase.Performed)
            {
                // EventBus 分发
                EventBus.Raise(new InputInteractEvent());
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                // EventBus 分发
                EventBus.Raise(new InputInteractCanceledEvent());
            }
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            if (context.phase == InputActionPhase.Performed)
            {
                // EventBus 分发
                EventBus.Raise(new InputJumpEvent());
            }
        }

        // 其他暂不使用的接口需实现为空，以满足接口定义
        public void OnLook(InputAction.CallbackContext context) { }
        public void OnAttack(InputAction.CallbackContext context) { }
        public void OnCrouch(InputAction.CallbackContext context) { }
        public void OnPrevious(InputAction.CallbackContext context) { }
        public void OnNext(InputAction.CallbackContext context) { }
        public void OnSprint(InputAction.CallbackContext context) { }

        // --- UI Map 回调 ---
        // 目前打造系统只要求 Cancel（ESC）关闭能力，其他 UI 回调按需留空实现。
        public void OnNavigate(InputAction.CallbackContext context) { }
        public void OnSubmit(InputAction.CallbackContext context) { }
        public void OnPoint(InputAction.CallbackContext context) { }
        public void OnClick(InputAction.CallbackContext context) { }
        public void OnRightClick(InputAction.CallbackContext context) { }
        public void OnMiddleClick(InputAction.CallbackContext context) { }
        public void OnScrollWheel(InputAction.CallbackContext context) { }
        public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
        public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }

        /// <summary>
        /// UI Cancel 回调：
        /// 通过 UI Action Map 的 Cancel 动作统一分发（键盘 ESC、手柄取消键等）。
        /// </summary>
        public void OnCancel(InputAction.CallbackContext context)
        {
            if (_currentMode == InputMode.Disabled) return;
            if (context.phase != InputActionPhase.Performed) return;
            UICancelEvent?.Invoke();
        }

        #endregion
    }
}
