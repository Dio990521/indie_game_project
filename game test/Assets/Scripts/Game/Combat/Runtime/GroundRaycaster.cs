using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 鼠标屏幕坐标 → 地面世界点的射线工具（静态、无状态）：
    /// 上场放置指示器（及 Phase 2 道具瞄准）共用。
    /// </summary>
    public static class GroundRaycaster
    {
        /// <summary>
        /// 从相机经屏幕点发射射线打地面层。
        /// </summary>
        /// <param name="camera">渲染相机（战斗中即 Cinemachine Brain 所在的主相机）</param>
        /// <param name="screenPosition">屏幕坐标（GameInputReader.CurrentPointerPosition）</param>
        /// <param name="groundMask">地面层</param>
        /// <param name="maxDistance">射线最大距离</param>
        /// <param name="point">命中的世界点</param>
        /// <returns>true = 命中地面</returns>
        public static bool TryGetGroundPoint(
            Camera camera,
            Vector2 screenPosition,
            LayerMask groundMask,
            float maxDistance,
            out Vector3 point)
        {
            point = default;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }
            return false;
        }
    }
}
