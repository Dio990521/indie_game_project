using UnityEngine;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 失败结算状态：
    /// 主角阵亡触发。停止战斗、广播 CombatEndedEvent，停留结算时长后进入退出流程。
    /// </summary>
    public class CombatDefeatState : CombatState
    {
        private float _exitTime;

        public override void OnEnter(CombatManager context)
        {
            context.EndBattle(false);
            _exitTime = Time.time + context.GetResultScreenDuration();
        }

        public override void OnUpdate(CombatManager context)
        {
            if (Time.time < _exitTime) return;
            context.ChangeState(new CombatExitState());
        }
    }
}
