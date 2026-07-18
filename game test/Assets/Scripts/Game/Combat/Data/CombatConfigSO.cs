using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗全局参数配置：
    /// 集中管理索敌/寻路节流间隔、放置规则与瞄准射线层级等调优参数。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Combat Config")]
    public class CombatConfigSO : ScriptableObject
    {
        [Header("自动战斗节流")]
        [Tooltip("索敌重新评估间隔（秒）：越小反应越快，开销越高")]
        public float RetargetInterval = 0.25f;

        [Tooltip("追击目标时的重寻路间隔（秒）：避免逐帧 SetDestination")]
        public float RepathInterval = 0.2f;

        [Header("上场放置")]
        [Tooltip("放置落点距主角战斗体的最大半径（米）——同时是手柄摇杆推满时的落点距离")]
        public float DeployPlacementRadius = 8f;

        [Tooltip("落点 NavMesh 采样容差（米）：落点在此距离内能吸附到 NavMesh 即视为合法")]
        public float PlacementSampleDistance = 1.5f;

        [Header("瞄准/放置射线")]
        [Tooltip("鼠标指向地面的射线层（战斗场景地面所在 Layer）")]
        public LayerMask GroundMask = ~0;

        [Tooltip("地面射线最大距离")]
        public float GroundRayDistance = 200f;

        [Header("结算")]
        [Tooltip("胜利/失败结算画面停留秒数（此后返回棋盘或停在测试场景）")]
        public float ResultScreenDuration = 2f;
    }
}
