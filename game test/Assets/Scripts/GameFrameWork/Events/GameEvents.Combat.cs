using UnityEngine;
using IndieGame.Gameplay.Combat;

namespace IndieGame.Core
{
    // 战斗系统事件：战斗流程、名册调度、充能与技能释放
    // （与其他 GameEvents.*.cs 一样按领域拆分；事件结构体引用 Gameplay 类型
    // 的先例见 GameEvents.Board.cs 的 BoardEntityInteractionEvent。）

    // ===================== 战斗流程 =====================

    /// <summary>
    /// 战斗开始事件：
    /// CombatInitState 完成初始生成后广播，HUD 据此构建名册槽位。
    /// </summary>
    public struct CombatStartedEvent
    {
        // 本场遭遇配置
        public EncounterSO Encounter;
        // 名册（含主角与后台成员）
        public CombatRoster Roster;
    }

    /// <summary>
    /// 战斗结束事件：
    /// 进入胜利/失败结算时广播。
    /// </summary>
    public struct CombatEndedEvent
    {
        // true = 胜利，false = 失败
        public bool Victory;
    }

    /// <summary>
    /// 战斗单位生成事件（我方上场与敌人刷新都会触发）。
    /// </summary>
    public struct CombatUnitSpawnedEvent
    {
        public CombatUnit Unit;
    }

    /// <summary>
    /// 战斗单位死亡事件：
    /// 由 CombatManager 对 DeathEvent 做幂等过滤后广播（保证每个单位只触发一次）。
    /// </summary>
    public struct CombatUnitDiedEvent
    {
        public CombatUnit Unit;
    }

    // ===================== 名册与调度 =====================

    /// <summary>
    /// 名册选择指针变更事件：
    /// 玩家用 Tab/LB/RB 切换选中角色后广播，HUD 移动选择指针。
    /// </summary>
    public struct RosterSelectionChangedEvent
    {
        // 选中槽位索引（0 = 主角）
        public int SelectedIndex;
        // 选中的名册成员
        public RosterMember Member;
    }

    /// <summary>
    /// 角色上场完成事件（放置确认并生成战斗体后广播）。
    /// </summary>
    public struct UnitDeployedEvent
    {
        public RosterMember Member;
        public CombatUnit Unit;
        public Vector3 Position;
    }

    /// <summary>
    /// 角色下场（回收）事件：回收即时生效，冷却开始计时。
    /// </summary>
    public struct UnitRecalledEvent
    {
        public RosterMember Member;
    }

    /// <summary>
    /// 进入上场放置态事件（HUD 可显示操作提示）。
    /// </summary>
    public struct DeployPlacementStartedEvent
    {
        public RosterMember Member;
    }

    /// <summary>
    /// 放置态结束事件（确认或取消都会触发；确认另有 UnitDeployedEvent）。
    /// </summary>
    public struct DeployPlacementEndedEvent
    {
        // true = 已确认上场，false = 取消
        public bool Confirmed;
    }

    /// <summary>
    /// 上场/下场操作被拒绝事件（HUD 抖动提示）。
    /// </summary>
    public struct DeployRejectedEvent
    {
        public RosterMember Member;
        public DeployRejectReason Reason;
    }

    /// <summary>
    /// 重上场冷却跳动事件：
    /// 按整秒节流广播（Remaining 变化到新的整秒才发），供 HUD 冷却圈刷新。
    /// </summary>
    public struct RedeployCooldownTickEvent
    {
        public RosterMember Member;
        // 剩余冷却秒数（0 = 冷却结束）
        public float Remaining;
    }

    // ===================== 充能与技能 =====================

    /// <summary>
    /// 武器充能变化事件：
    /// 按整数百分比节流广播（避免逐帧刷 UI）。
    /// </summary>
    public struct UnitChargeChangedEvent
    {
        // 归属单位的 GameObject（与 HealthChangedEvent.Owner 对齐的过滤方式）
        public GameObject Owner;
        public float Current;
        public float Max;
    }

    /// <summary>
    /// 技能释放完成事件（充能已清零）。
    /// </summary>
    public struct SkillReleasedEvent
    {
        public CombatUnit Unit;
        public SkillSO Skill;
    }

    /// <summary>
    /// 技能释放被拒绝事件（充能未满/不在场等，HUD 抖动提示）。
    /// </summary>
    public struct SkillCastRejectedEvent
    {
        public RosterMember Member;
        public SkillCastRejectReason Reason;
    }
}
