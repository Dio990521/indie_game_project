using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗道具配置基类（命令模式，与 SkillSO / BoardEventSO 的项目惯例一致）：
    /// 道具只在战斗内由后台角色生产、战斗结束清空（不进背包、不进存档）。
    /// - 需瞄准的道具（RequiresAiming=true）：数字键进入瞄准态选点，再按同键确认后 Execute；
    /// - 即时道具（RequiresAiming=false）：数字键按下直接 Execute（落点参数为主角位置）。
    /// 子类实现 Execute 完成具体效果（聚怪/治疗领域/土墙/复活等）。
    /// </summary>
    public abstract class CombatItemSO : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("道具唯一 ID（日志/统计用）")]
        public string ID;

        [Tooltip("显示名（HUD 道具栏用）")]
        public string DisplayName;

        [Tooltip("图标（HUD 道具栏显示）")]
        public Sprite Icon;

        [Tooltip("道具描述")]
        [TextArea]
        public string Description;

        [Header("生产")]
        [Tooltip("后台角色生产一个所需秒数")]
        public float ProductionTime = 10f;

        [Header("携带")]
        [Tooltip("该种道具的携带数量上限（道具栏同类槽位的堆叠上限）")]
        public int CarryLimit = 3;

        [Header("使用")]
        [Tooltip("是否需要瞄准选点（false = 按键即时生效，如复活道具）")]
        public bool RequiresAiming = true;

        [Tooltip("瞄准选点的最大半径（以主角为圆心；RequiresAiming 时有效）")]
        public float CastRange = 8f;

        /// <summary>
        /// 使用前置校验（消耗道具前调用）：
        /// 返回 false 表示当前条件不满足（如复活道具但无阵亡角色），不消耗并提示玩家。
        /// </summary>
        public virtual bool CanUse(CombatManager manager)
        {
            return true;
        }

        /// <summary>
        /// 执行道具效果：
        /// 由 CombatManager.UseItem 在消耗成功后调用。
        /// </summary>
        /// <param name="manager">战斗管理器（提供 Registry/Roster/对象池访问）</param>
        /// <param name="point">使用落点（即时道具为主角当前位置）</param>
        public abstract void Execute(CombatManager manager, Vector3 point);
    }
}
