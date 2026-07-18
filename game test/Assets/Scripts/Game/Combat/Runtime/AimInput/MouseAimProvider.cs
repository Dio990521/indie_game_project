using UnityEngine;
using IndieGame.Core.Input;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 鼠标指向实现：
    /// 用缓存的指针屏幕坐标做地面射线，命中点 Clamp 到基准点半径圆内。
    /// 鼠标只负责"移动指示器"，确认/取消由按键完成（不使用鼠标点击）。
    /// </summary>
    public class MouseAimProvider : IAimInputProvider
    {
        private readonly GameInputReader _input;
        private readonly CombatConfigSO _config;
        private Camera _camera;

        public MouseAimProvider(GameInputReader input, CombatConfigSO config)
        {
            _input = input;
            _config = config;
        }

        public bool TryGetPoint(Vector3 origin, float radius, out Vector3 point)
        {
            point = default;
            if (_input == null || _config == null) return false;

            // 相机惰性缓存：战斗渲染相机即 Cinemachine Brain 所在的主相机
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;

            if (!GroundRaycaster.TryGetGroundPoint(
                    _camera, _input.CurrentPointerPosition, _config.GroundMask, _config.GroundRayDistance, out Vector3 hit))
            {
                return false;
            }

            point = ClampToRadius(origin, hit, radius);
            return true;
        }

        /// <summary>
        /// 把落点约束到以 origin 为圆心的水平圆内（保持命中点高度）。
        /// </summary>
        private static Vector3 ClampToRadius(Vector3 origin, Vector3 target, float radius)
        {
            Vector3 offset = target - origin;
            float y = offset.y;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius)
            {
                offset = offset.normalized * radius;
            }
            Vector3 result = origin + offset;
            result.y = origin.y + y;
            return result;
        }
    }
}
