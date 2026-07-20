using UnityEngine;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 战斗进行状态：
    /// 驱动波次刷怪计时与重上场冷却广播；胜负主要由死亡事件驱动，
    /// 此处仅做兜底轮询（防止事件顺序边界导致漏判）。
    /// </summary>
    public class CombatActiveState : CombatState
    {
        public override void OnUpdate(CombatManager context)
        {
            if (!context.BattleRunning) return;

            // 波次刷怪计时
            context.TickWaveSpawning();
            // 名册冷却广播（整秒节流）
            context.Roster.TickCooldownBroadcast();
            // 后台成员的道具生产推进
            context.ItemProduction.Tick(Time.deltaTime);
            // 胜负兜底轮询
            context.CheckBattleOutcome();
        }
    }
}
