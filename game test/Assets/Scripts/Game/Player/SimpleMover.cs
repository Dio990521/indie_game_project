using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Player
{
    /// <summary>
    /// 基础移动组件：负责玩家在自由探索模式下的位移、旋转和动画控制。
    /// 该脚本通过 CharacterController 实现物理位移，并支持相机坐标系转换。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleMover : MonoBehaviour
    {
        [Header("基础配置")]
        [Tooltip("基础移动速度")]
        public float moveSpeed = 5f;
        [Tooltip("旋转平滑速度（值越大转向越快）")]
        public float rotateSpeed = 15f;
        [Tooltip("Animator 中控制移动速度的参数名称")]
        public string speedParamName = "Speed";

        [SerializeField]
        [Tooltip("角色属性系统引用，用于获取动态增长的速度属性")]
        private CharacterStats stats;

        // --- 内部组件引用 ---
        private CharacterController _controller; // Unity 自带的角色控制器组件
        private Animator _animator;              // 动画机引用
        private Transform _cameraTransform;      // 相机的 Transform 指向，用于参考方向

        // --- 运行状态变量 ---
        private Vector2 _currentInputVector;     // 存储当前帧的玩家输入向量（WASD或摇杆）
        private int _animIDSpeed;                // 缓存动画参数 ID，提升性能

        // 状态锁：控制当前角色是否可以移动（例如在棋盘模式或对话中会被锁住）
        private bool _canMove = true;

        private void Awake()
        {
            // 1. 初始化引用
            _controller = GetComponent<CharacterController>();
            // 注意：通常模型在子物体上，所以使用 GetComponentInChildren
            _animator = GetComponentInChildren<Animator>();

            // 如果面板上没拖拽，尝试从自身物体获取属性组件
            if (stats == null) stats = GetComponent<CharacterStats>();

            // 缓存主相机引用
            if (Camera.main != null) _cameraTransform = Camera.main.transform;

            // 2. [性能优化] 预先将字符串转为哈希 ID，避免在 Update 这种高频方法里直接用字符串查动画参数
            _animIDSpeed = Animator.StringToHash(speedParamName);

            // 3. 订阅全局状态变更事件：用于在“探索模式”和“棋盘模式”切换时自动启用/禁用移动
            EventBus.Subscribe<GameStateChangedEvent>(HandleStateChanged);
        }

        private void OnDestroy()
        {
            // 脚本销毁时务必取消订阅，防止内存泄漏或无效回调
            EventBus.Unsubscribe<GameStateChangedEvent>(HandleStateChanged);
        }

        private void OnEnable()
        {
            // 订阅输入事件（EventBus）
            EventBus.Subscribe<InputMoveEvent>(OnMoveInput);
        }

        private void OnDisable()
        {
            // 禁用脚本时停止监听输入
            EventBus.Unsubscribe<InputMoveEvent>(OnMoveInput);
        }

        private void Start()
        {
            // 设置相机跟随目标：一旦角色入场，让相机系统锁定此物体
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(this.transform);
            }

            // [鲁棒性检查] 初始化时手动触发一次状态同步，确保角色状态与当前游戏状态一致
            if (GameManager.Instance != null)
            {
                HandleStateChanged(new GameStateChangedEvent { NewState = GameManager.Instance.CurrentState });
            }
        }

        /// <summary>
        /// 处理游戏模式变更：
        /// 只有在 FreeRoam (自由探索) 模式下才允许角色自由走动。
        /// </summary>
        private void HandleStateChanged(GameStateChangedEvent evt)
        {
            GameState newState = evt.NewState;
            if (newState == GameState.FreeRoam)
            {
                _canMove = true;
                _controller.enabled = true; // 启用物理控制器
            }
            else
            {
                // 如果切换到其他模式（如下棋、过场、战斗）：
                _canMove = false;
                _controller.enabled = false; // 禁用物理控制器，防止与其他位移冲突

                // 重置状态：清除残余输入并让动画归零
                _currentInputVector = Vector2.zero;
                if (_animator) _animator.SetFloat(_animIDSpeed, 0);
            }
        }

        /// <summary>
        /// 输入系统回调：接收并更新当前输入向量。
        /// </summary>
        private void OnMoveInput(InputMoveEvent evt)
        {
            // 设计注记：即使在锁定状态（_canMove = false）也记录输入，
            // 只是在 Update 阶段不执行逻辑，这能保证恢复移动时手感的连贯性。
            _currentInputVector = evt.Value;
        }

        private void Update()
        {
            // 如果当前没有移动权限（被锁住），则不处理后续的位移和动画更新
            if (!_canMove) return;

            MovePlayer();     // 处理位移与转向
            UpdateAnimation(); // 处理动画参数更新
        }

        /// <summary>
        /// 核心移动逻辑：实现相对于相机坐标系的平滑位移。
        /// </summary>
        private void MovePlayer()
        {
            // 如果输入太小（死区检测），则不执行逻辑
            if (_currentInputVector.magnitude < 0.1f) return;

            // 动态寻找主相机引用（兜底逻辑）
            if (_cameraTransform == null)
            {
                if (Camera.main != null) _cameraTransform = Camera.main.transform;
                if (_cameraTransform == null) return;
            }

            // --- 坐标系转换 ---
            // 将输入的方向投影到相机的平面。
            // 这样做可以让“按 W 向上”始终等于“朝着相机前方走”，而不是朝着世界坐标轴走。
            Vector3 forward = _cameraTransform.forward;
            Vector3 right = _cameraTransform.right;

            // 强制抹除 Y 轴（高度）干扰，确保角色只在地面平面移动，不会因为相机朝下看而“钻地”
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // 计算最终的移动方向向量：相机的前方向 * 输入Y + 相机的右方向 * 输入X
            Vector3 moveDir = (forward * _currentInputVector.y + right * _currentInputVector.x).normalized;

            // --- 速度计算 ---
            // 优先从 RPG 属性系统中取值，如果没有挂载 Stats 组件则使用默认配置的速度
            float speed = stats != null ? stats.MoveSpeed.Value : moveSpeed;

            // 执行物理位移：速度 * 时间步长
            _controller.Move(moveDir * speed * Time.deltaTime);

            // --- 转向处理 ---
            // 如果有移动方向，让角色平滑地转向该方向
            if (moveDir != Vector3.zero)
            {
                // 计算目标朝向的四元数
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                // 使用 Slerp (球面线性插值) 实现平滑旋转，防止角色瞬间生硬转向
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 动画同步：将输入的幅度传递给 Animator 混合树。
        /// </summary>
        private void UpdateAnimation()
        {
            if (_animator == null) return;

            // 获取输入的模长（0 到 1 之间），对应动画中 Idle -> Walk -> Run 的平滑过渡
            float currentSpeed = _currentInputVector.magnitude;

            // 设置动画机参数
            _animator.SetFloat(_animIDSpeed, currentSpeed);
        }
    }
}
