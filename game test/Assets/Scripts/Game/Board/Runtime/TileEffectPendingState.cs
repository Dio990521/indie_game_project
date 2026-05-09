namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 特殊格子效果的待执行状态集合：
    /// 将 BoardMovementController 中原本散乱的 7 组 _pending* 字段统一聚合，
    /// 便于整体重置（赋值 Default）和集中阅读，也方便未来扩展新格子类型。
    ///
    /// 生命周期：每次 BeginMove 时通过 Default 重置，移动过程中由各格子事件写入。
    /// </summary>
    internal struct TileEffectPendingState
    {
        // [前进/后退格] 额外步数；正数前进，负数后退
        public int ExtraSteps;

        // [扭曲格] 下一步强制走向的目标节点 ID；-1 表示无强制方向
        public int ForcedNextNodeId;

        // [扭曲格] 下次分叉选择时需要过滤掉的被保护节点 ID；-1 表示无过滤
        public int ProtectedNodeId;

        // [人体大炮] 是否有待执行的弹射请求及参数
        public bool CannonLaunch;
        public float CannonArcHeight;
        public float CannonLaunchSpeed;

        // [传送格] 是否有待执行的传送请求及目标节点
        public bool Teleport;
        public int TeleportTargetId;

        // [方向格] 首步强制走向节点 ID 及移动步数；-1/0 表示无待执行请求
        public int DirectionalNodeId;
        public int DirectionalSteps;

        /// <summary>
        /// 回合开始时的默认（干净）状态。
        /// </summary>
        public static TileEffectPendingState Default => new TileEffectPendingState
        {
            ExtraSteps        = 0,
            ForcedNextNodeId  = -1,
            ProtectedNodeId   = -1,
            CannonLaunch      = false,
            CannonArcHeight   = 5f,
            CannonLaunchSpeed = 12f,
            Teleport          = false,
            TeleportTargetId  = -1,
            DirectionalNodeId = -1,
            DirectionalSteps  = 0
        };
    }
}
