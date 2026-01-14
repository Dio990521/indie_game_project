using UnityEngine;

namespace IndieGame.Core.Utilities
{
    public static class BezierUtils
    {
        /// <summary>
        /// 二阶贝塞尔曲线计算
        /// </summary>
        public static Vector3 GetQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            t = Mathf.Clamp01(t);
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * p0 +
                   2f * oneMinusT * t * p1 +
                   t * t * p2;
        }
        
        // 未来可以在这里扩展三阶贝塞尔或其他曲线算法
    }
}