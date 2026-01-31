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
    public class GameInputReader : ScriptableObject, InputSystem_Actions.IPlayerActions
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

        private InputSystem_Actions _gameInput;
        private InputMode _currentMode = InputMode.Gameplay;

        private void OnEnable()
        {
            if (_gameInput == null)
            {
                _gameInput = new InputSystem_Actions();
                
                // 注册回调接口到 InputSystem
                _gameInput.Player.SetCallbacks(this);
            }
            
            // 默认开启输入
            EnableGameplayInput();
        }

        private void OnDisable()
        {
            DisableAllInput();
        }

        /// <summary>
        /// 开启 Gameplay 输入。
        /// </summary>
        public void EnableGameplayInput() => _gameInput.Player.Enable();

        /// <summary>
        /// 禁用所有输入。
        /// </summary>
        public void DisableAllInput() => _gameInput.Player.Disable();

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
                    // 允许游戏输入
                    _gameInput.Player.Enable();
                    break;
                case InputMode.UIOnly:
                    // 禁用游戏输入，并清空移动
                    _gameInput.Player.Disable();
                    CurrentMoveInput = Vector2.zero;
                    EventBus.Raise(new InputMoveEvent { Value = Vector2.zero });
                    break;
                case InputMode.Disabled:
                    // 完全禁用输入，并清空移动
                    _gameInput.Player.Disable();
                    CurrentMoveInput = Vector2.zero;
                    EventBus.Raise(new InputMoveEvent { Value = Vector2.zero });
                    break;
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

        #endregion
    }
}
