using UnityEngine;
using IndieGame.Core.Input;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 指向输入路由（"最后活跃输入胜出"）：
    /// 同时持有鼠标与摇杆两个 Provider——
    /// 鼠标产生位移则鼠标接管，摇杆推量超死区则摇杆接管，
    /// 玩家无需任何显式切换即可在键鼠/手柄间无缝换手。
    /// 由放置态（DeployPlacementState）持有并在 OnUpdate 中喂入输入事件。
    /// </summary>
    public class AimInputRouter
    {
        private enum ActiveSource
        {
            Mouse,
            Stick
        }

        // 鼠标位移判定阈值（像素）：过滤传感器噪声
        private const float MouseMoveThreshold = 2f;
        // 摇杆接管的推量死区
        private const float StickTakeoverDeadzone = 0.2f;

        private readonly MouseAimProvider _mouseProvider;
        private readonly StickAimProvider _stickProvider;

        private ActiveSource _active = ActiveSource.Mouse;
        private Vector2 _lastPointerPosition;
        private bool _pointerInitialized;

        public AimInputRouter(GameInputReader input, CombatConfigSO config)
        {
            _mouseProvider = new MouseAimProvider(input, config);
            _stickProvider = new StickAimProvider(input);
        }

        /// <summary>
        /// 喂入指针位置（订阅 InputPointEvent 的调用方转发）：位移超阈值则鼠标接管。
        /// </summary>
        public void NotifyPointer(Vector2 screenPosition)
        {
            if (!_pointerInitialized)
            {
                _pointerInitialized = true;
                _lastPointerPosition = screenPosition;
                return;
            }
            if ((screenPosition - _lastPointerPosition).sqrMagnitude >= MouseMoveThreshold * MouseMoveThreshold)
            {
                _active = ActiveSource.Mouse;
            }
            _lastPointerPosition = screenPosition;
        }

        /// <summary>
        /// 喂入摇杆输入（订阅 InputAimStickEvent 的调用方转发）：推量超死区则摇杆接管。
        /// </summary>
        public void NotifyStick(Vector2 stickValue)
        {
            if (stickValue.sqrMagnitude >= StickTakeoverDeadzone * StickTakeoverDeadzone)
            {
                _active = ActiveSource.Stick;
            }
        }

        /// <summary>
        /// 按当前活跃设备解析世界落点。
        /// </summary>
        public bool TryGetPoint(Vector3 origin, float radius, out Vector3 point)
        {
            return _active == ActiveSource.Stick
                ? _stickProvider.TryGetPoint(origin, radius, out point)
                : _mouseProvider.TryGetPoint(origin, radius, out point);
        }
    }
}
