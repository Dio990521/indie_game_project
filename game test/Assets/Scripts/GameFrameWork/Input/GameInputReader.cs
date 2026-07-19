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

        // 缓存指针（鼠标）最新的屏幕坐标：
        // 战斗放置态每帧需要指针位置做地面射线，事件驱动 + 缓存可避免逻辑层轮询 InputSystem
        public Vector2 CurrentPointerPosition { get; private set; }

        // 缓存手柄右摇杆的最新输入值（回中时为零向量），供放置态指向逻辑读取
        public Vector2 CurrentAimStick { get; private set; }

        /// <summary>
        /// UI 取消事件（通常由 ESC / 手柄 B / Cancel 输入触发）：
        /// UI 控制器（如打造界面）应通过订阅/注销该事件来实现“安全关闭”。
        /// </summary>
        public event Action UICancelEvent;

        private InputSystem_Actions _gameInput;
        private InputMode _currentMode = InputMode.Gameplay;
        private int _inputLockCount;
        private InputMode _modeBeforeLock = InputMode.Gameplay;

        // --- 战斗扩展输入（程序化 InputAction 组） ---
        // 说明：战斗玩法（技能/上下场/名册切换/摇杆指向）的按键以代码方式创建，
        // 不修改 InputSystem_Actions.inputactions 资产与其生成代码：
        // 1) 生成代码由 Unity 自动重写，手改资产+生成类需要两处同步，风险高；
        // 2) 程序化 action 编译即生效，绑定集中在此处一目了然；
        // 3) 后续若需要在编辑器里可视化配置，可整体迁移进资产的 Player Map（回调签名不变）。
        private InputAction _combatSkillAction;      // 技能：键盘 Q / 手柄 X（West）
        private InputAction _combatDeployAction;     // 上场/下场/放置确认：键盘 E / 手柄 Y（North）
        private InputAction _combatSelectNextAction; // 选中下一个：Tab（按住 Shift 反向）/ 手柄 RB
        private InputAction _combatSelectPrevAction; // 选中上一个：手柄 LB（键盘走 Shift+Tab）
        private InputAction _combatAimStickAction;   // 指向：手柄右摇杆

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

            // 创建战斗扩展输入（幂等：仅首次创建）
            EnsureCombatActions();

            // 默认开启输入
            EnableGameplayInput();
            // M6 修复：幂等订阅。
            // ScriptableObject 的 OnEnable 由资源加载触发（与场景生命周期无关），
            // 关闭 Domain Reload 的快速播放模式下可能重复触发而 OnDisable 未成对执行，
            // 先退订再订阅可防止委托链重复导致输入事件被派发两次。
            EventBus.Unsubscribe<InputLockRequestedEvent>(HandleInputLockRequested);
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
            SetCombatActionsEnabled(false);
        }

        /// <summary>
        /// 创建战斗扩展输入 action（对象创建幂等，回调绑定每次 OnEnable 都重新做一遍）：
        /// 绑定与回调集中在此，enable/disable 跟随 Player Map（见 SetInputMode）。
        ///
        /// M9 修复：旧实现用"_combatSkillAction != null 就直接 return"作为"回调已绑好"的
        /// 替代判断，但实测发现——脚本重新编译触发的域重载会清空这些"游离"InputAction
        /// （未挂在任何 InputActionAsset 下）内部的回调委托列表，而 C# 字段引用本身不会变成
        /// null（对象仍存活，Enable/绑定路径都正常），导致旧 guard 误判"已初始化"从而
        /// 永久跳过重新绑定——按键从此再无反应，且没有任何报错，只能靠反射查
        /// InputAction 内部 m_OnPerformed 才能发现列表长度为 0。
        /// 修复方式：对象创建仍然幂等（避免重复 new/AddBinding），但回调统一走
        /// "先退订再订阅"（与本文件 InputLockRequestedEvent 订阅的既有防重复模式一致），
        /// 确保无论 OnEnable 触发几次、域重载清没清过回调，最终都精确保留一份订阅。
        /// </summary>
        private void EnsureCombatActions()
        {
            if (_combatSkillAction == null)
            {
                // 技能键：Q / 手柄 X
                _combatSkillAction = new InputAction("CombatSkill", InputActionType.Button);
                _combatSkillAction.AddBinding("<Keyboard>/q");
                _combatSkillAction.AddBinding("<Gamepad>/buttonWest");

                // 上场/下场/放置确认键：E / 手柄 Y
                _combatDeployAction = new InputAction("CombatDeploy", InputActionType.Button);
                _combatDeployAction.AddBinding("<Keyboard>/e");
                _combatDeployAction.AddBinding("<Gamepad>/buttonNorth");

                // 名册切换：Tab（Shift+Tab 反向，方向在回调里判定）/ 手柄 RB
                _combatSelectNextAction = new InputAction("CombatSelectNext", InputActionType.Button);
                _combatSelectNextAction.AddBinding("<Keyboard>/tab");
                _combatSelectNextAction.AddBinding("<Gamepad>/rightShoulder");

                // 名册反向切换：手柄 LB（键盘由 Shift+Tab 覆盖）
                _combatSelectPrevAction = new InputAction("CombatSelectPrev", InputActionType.Button);
                _combatSelectPrevAction.AddBinding("<Gamepad>/leftShoulder");

                // 指向摇杆：手柄右摇杆（Value 型，回中触发 canceled 清零）
                _combatAimStickAction = new InputAction("CombatAimStick", InputActionType.Value, expectedControlType: "Vector2");
                _combatAimStickAction.AddBinding("<Gamepad>/rightStick");
            }

            // 回调绑定：无条件先退订再订阅，防止漏绑（域重载清空回调）或重复绑（同一次 OnEnable 内误触发两次）
            _combatSkillAction.performed -= HandleCombatSkill;
            _combatSkillAction.performed += HandleCombatSkill;

            _combatDeployAction.performed -= HandleCombatDeploy;
            _combatDeployAction.performed += HandleCombatDeploy;

            _combatSelectNextAction.performed -= HandleCombatSelectNext;
            _combatSelectNextAction.performed += HandleCombatSelectNext;

            _combatSelectPrevAction.performed -= HandleCombatSelectPrev;
            _combatSelectPrevAction.performed += HandleCombatSelectPrev;

            _combatAimStickAction.performed -= HandleCombatAimStick;
            _combatAimStickAction.performed += HandleCombatAimStick;
            _combatAimStickAction.canceled -= HandleCombatAimStick;
            _combatAimStickAction.canceled += HandleCombatAimStick;
        }

        /// <summary>
        /// 统一开关战斗扩展输入（跟随 Player Map 的启用状态）。
        /// </summary>
        private void SetCombatActionsEnabled(bool enabled)
        {
            if (_combatSkillAction == null) return;
            if (enabled)
            {
                _combatSkillAction.Enable();
                _combatDeployAction.Enable();
                _combatSelectNextAction.Enable();
                _combatSelectPrevAction.Enable();
                _combatAimStickAction.Enable();
            }
            else
            {
                _combatSkillAction.Disable();
                _combatDeployAction.Disable();
                _combatSelectNextAction.Disable();
                _combatSelectPrevAction.Disable();
                _combatAimStickAction.Disable();
                CurrentAimStick = Vector2.zero;
            }
        }

        // --- 战斗扩展输入回调 ---

        private void HandleCombatSkill(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            EventBus.Raise(new InputSkillEvent());
        }

        private void HandleCombatDeploy(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            EventBus.Raise(new InputDeployEvent());
        }

        private void HandleCombatSelectNext(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            // 键盘惯例：按住 Shift 时 Tab 反向切换
            bool shiftHeld = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            EventBus.Raise(new InputSelectEvent { Direction = shiftHeld ? -1 : 1 });
        }

        private void HandleCombatSelectPrev(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            EventBus.Raise(new InputSelectEvent { Direction = -1 });
        }

        private void HandleCombatAimStick(InputAction.CallbackContext context)
        {
            if (_currentMode != InputMode.Gameplay) return;
            CurrentAimStick = context.ReadValue<Vector2>();
            EventBus.Raise(new InputAimStickEvent { Value = CurrentAimStick });
        }

        /// <summary>
        /// 设置输入模式：
        /// 会根据模式启用/禁用 Player 输入，并在必要时清零移动输入。
        /// M6 修复：输入锁定期间（加载/黑屏）外部请求的模式切换不立即生效，
        /// 而是更新"解锁后应恢复的模式"。旧实现会在解锁时用锁定前的旧模式
        /// 覆盖锁定期间的合法切换（例如加载中打开了 UI，解锁后却恢复成 Gameplay）。
        /// </summary>
        public void SetInputMode(InputMode mode)
        {
            if (_inputLockCount > 0 && mode != InputMode.Disabled)
            {
                _modeBeforeLock = mode;
                return;
            }

            _currentMode = mode;
            switch (mode)
            {
                case InputMode.Gameplay:
                    // Gameplay：允许玩家输入，同时保留 UI Cancel（ESC）监听能力
                    _gameInput.Player.Enable();
                    _gameInput.UI.Enable();
                    // 战斗扩展输入跟随 Player Map 启用
                    SetCombatActionsEnabled(true);
                    break;
                case InputMode.UIOnly:
                    // UIOnly：禁用玩家输入，但保留 UI 输入（用于菜单导航/取消）
                    _gameInput.Player.Disable();
                    _gameInput.UI.Enable();
                    SetCombatActionsEnabled(false);
                    CurrentMoveInput = Vector2.zero;
                    EventBus.Raise(new InputMoveEvent { Value = Vector2.zero });
                    break;
                case InputMode.Disabled:
                    // Disabled：完全禁用输入（Player + UI）
                    _gameInput.Player.Disable();
                    _gameInput.UI.Disable();
                    SetCombatActionsEnabled(false);
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

        /// <summary>
        /// 指针位置回调（UI Map 的 Point，鼠标移动时持续触发）：
        /// 缓存屏幕坐标并广播，供战斗放置指示器等逻辑消费。
        /// EventBus.Raise 内部为零分配（缓存调用快照），逐帧广播安全。
        /// </summary>
        public void OnPoint(InputAction.CallbackContext context)
        {
            if (_currentMode == InputMode.Disabled) return;
            CurrentPointerPosition = context.ReadValue<Vector2>();
            EventBus.Raise(new InputPointEvent { ScreenPosition = CurrentPointerPosition });
        }

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
