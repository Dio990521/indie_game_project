using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace IndieGame.Core.Input
{
    /// <summary>
    /// 输入读取器：充当输入系统和游戏逻辑之间的中间层。
    /// 所有的输入事件都通过 C# Action 分发，逻辑脚本不再直接依赖 InputSystem_Actions。
    /// </summary>
    [CreateAssetMenu(fileName = "GameInputReader", menuName = "IndieGame/Core/Input Reader")]
    public class GameInputReader : ScriptableObject, InputSystem_Actions.IPlayerActions
    {
        public enum InputMode
        {
            Gameplay,
            UIOnly,
            Disabled
        }

        // === 游戏逻辑事件 ===
        public event Action<Vector2> MoveEvent;
        public event Action InteractEvent;
        public event Action JumpEvent;
        public event Action InteractCanceledEvent; // 比如松开按键

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

        public void EnableGameplayInput() => _gameInput.Player.Enable();
        public void DisableAllInput() => _gameInput.Player.Disable();

        public void SetInputMode(InputMode mode)
        {
            _currentMode = mode;
            switch (mode)
            {
                case InputMode.Gameplay:
                    _gameInput.Player.Enable();
                    break;
                case InputMode.UIOnly:
                    _gameInput.Player.Disable();
                    CurrentMoveInput = Vector2.zero;
                    MoveEvent?.Invoke(Vector2.zero);
                    break;
                case InputMode.Disabled:
                    _gameInput.Player.Disable();
                    CurrentMoveInput = Vector2.zero;
                    MoveEvent?.Invoke(Vector2.zero);
                    break;
            }
        }

        #region InputSystem Callbacks

        public void OnMove(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            CurrentMoveInput = context.ReadValue<Vector2>();
            MoveEvent?.Invoke(CurrentMoveInput);
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            if (context.phase == InputActionPhase.Performed)
                InteractEvent?.Invoke();
            else if (context.phase == InputActionPhase.Canceled)
                InteractCanceledEvent?.Invoke();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            if (context.phase == InputActionPhase.Performed)
                JumpEvent?.Invoke();
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
