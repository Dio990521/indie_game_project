using UnityEngine;
using IndieGame.Core.Input;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 手柄右摇杆指向实现：
    /// 落点 = 基准点 + 相机相对方向 × 推量 × 半径（推得越满打得越远，无需射线）。
    /// 摇杆回中时保持上一次的落点方向（推量归零则落点回到基准点附近）。
    /// </summary>
    public class StickAimProvider : IAimInputProvider
    {
        // 摇杆死区：低于该推量视为回中
        private const float Deadzone = 0.15f;

        private readonly GameInputReader _input;
        private Camera _camera;

        public StickAimProvider(GameInputReader input)
        {
            _input = input;
        }

        public bool TryGetPoint(Vector3 origin, float radius, out Vector3 point)
        {
            point = origin;
            if (_input == null) return false;

            Vector2 stick = _input.CurrentAimStick;
            if (stick.sqrMagnitude < Deadzone * Deadzone)
            {
                // 回中：落点即基准点（指示器停在角色脚下）
                return true;
            }

            if (_camera == null) _camera = Camera.main;

            // 与项目移动输入一致：把摇杆输入转换为相机相对的水平方向
            Vector3 forward;
            Vector3 right;
            if (_camera != null)
            {
                forward = _camera.transform.forward;
                right = _camera.transform.right;
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();
            }
            else
            {
                forward = Vector3.forward;
                right = Vector3.right;
            }

            float magnitude = Mathf.Clamp01(stick.magnitude);
            Vector3 dir = (right * stick.x + forward * stick.y).normalized;
            point = origin + dir * (magnitude * radius);
            return true;
        }
    }
}
