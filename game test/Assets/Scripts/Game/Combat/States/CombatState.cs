using IndieGame.Core;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 战斗状态基类：
    /// 上下文为 CombatManager（与棋盘的 BoardState : BaseState&lt;BoardGameManager&gt; 惯例一致）。
    /// </summary>
    public abstract class CombatState : BaseState<CombatManager>
    {
    }
}
