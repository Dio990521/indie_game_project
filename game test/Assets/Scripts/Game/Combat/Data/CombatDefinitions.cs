namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗阵营：
    /// 索敌、技能目标筛选均以阵营为单位。
    /// </summary>
    public enum CombatTeam
    {
        // 玩家方（主角与上场同伴）
        Player,
        // 敌方
        Enemy
    }

    /// <summary>
    /// 技能作用形态（v2 玩法：技能按键即放，方向/范围由施法者的位置与当前目标自动解析）：
    /// - SelfRadial：以施法者自身为圆心的圆形范围；
    /// - TargetCircle：以当前索敌目标为圆心的圆形范围（无目标时退化为 SelfRadial）；
    /// - LineToTarget：从施法者出发、朝当前目标方向的矩形直线（无目标时沿面朝方向）。
    /// </summary>
    public enum SkillShape
    {
        SelfRadial,
        TargetCircle,
        LineToTarget
    }

    /// <summary>
    /// 名册成员状态：
    /// Field = 在场作战；Backline = 后台待命（Phase 2 起负责生产道具）；Dead = 阵亡（本场不可再上场）。
    /// </summary>
    public enum RosterMemberState
    {
        Field,
        Backline,
        Dead
    }

    /// <summary>
    /// 技能释放被拒绝的原因（用于 HUD 提示）。
    /// </summary>
    public enum SkillCastRejectReason
    {
        // 选中角色不在场上
        NotOnField,
        // 武器充能未满
        ChargeNotFull,
        // 角色已阵亡
        MemberDead,
        // 该角色没有配置技能
        NoSkill
    }

    /// <summary>
    /// 上场/下场操作被拒绝的原因（用于 HUD 提示）。
    /// </summary>
    public enum DeployRejectReason
    {
        // 场上人数已达上限（含主角共 3 人）
        FieldFull,
        // 重上场冷却未结束
        CooldownNotReady,
        // 角色已阵亡
        MemberDead,
        // 主角不可下场
        ProtagonistCannotRecall,
        // 放置落点不合法（不在 NavMesh 上）
        InvalidPlacement
    }
}
