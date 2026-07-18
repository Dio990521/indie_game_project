using UnityEngine;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 胜利结算状态：
    /// 停止战斗（单位组件停摆）、广播 CombatEndedEvent，
    /// 停留结算时长后进入退出流程。
    /// </summary>
    public class CombatVictoryState : CombatState
    {
        private float _exitTime;

        public override void OnEnter(CombatManager context)
        {
            context.EndBattle(true);
            _exitTime = Time.time + context.GetResultScreenDuration();
        }

        public override void OnUpdate(CombatManager context)
        {
            if (Time.time < _exitTime) return;
            context.ChangeState(new CombatExitState());
        }
    }
}
