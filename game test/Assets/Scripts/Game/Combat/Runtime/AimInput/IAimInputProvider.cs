using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 指向输入抽象：
    /// 放置/瞄准逻辑只消费"以 origin 为圆心、radius 为半径范围内的世界落点"，
    /// 不关心输入设备。键鼠与手柄各有一个实现，由 AimInputRouter 按活跃设备路由。
    /// </summary>
    public interface IAimInputProvider
    {
        /// <summary>
        /// 尝试解析当前指向的世界落点。
        /// </summary>
        /// <param name="origin">基准点（放置态下为主角战斗体位置）</param>
        /// <param name="radius">最大半径（落点被 Clamp 到该圆内）</param>
        /// <param name="point">解析出的世界落点</param>
        /// <returns>true = 本帧能给出有效落点</returns>
        bool TryGetPoint(Vector3 origin, float radius, out Vector3 point);
    }
}
