using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 自动索敌组件：
    /// 按节流间隔从 CombatUnitRegistry 取"最近的敌对阵营存活单位"作为当前目标；
    /// 目标死亡/失效时立即重新索敌。无物理查询、零逐帧分配。
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoTargeting : MonoBehaviour
    {
        /// <summary> 当前索敌目标（可能为 null） </summary>
        public CombatUnit CurrentTarget { get; private set; }

        private CombatUnit _self;
        private float _retargetInterval = 0.25f;
        private float _nextRetargetTime;

        /// <summary>
        /// 初始化（由 CombatUnit.Initialize 调用）。
        /// </summary>
        public void Initialize(CombatUnit self, float retargetInterval)
        {
            _self = self;
            _retargetInterval = Mathf.Max(0.05f, retargetInterval);
            _nextRetargetTime = 0f;
            CurrentTarget = null;
        }

        private void Update()
        {
            CombatManager manager = CombatManager.Instance;
            if (manager == null || !manager.BattleRunning) return;
            if (_self == null || !_self.IsAlive) return;

            // 目标失效（死亡/下场）时立即重索敌；否则按间隔重评估最近目标
            bool targetInvalid = CurrentTarget == null || !CurrentTarget.IsAlive;
            if (!targetInvalid && Time.time < _nextRetargetTime) return;

            _nextRetargetTime = Time.time + _retargetInterval;
            CombatTeam enemyTeam = _self.Team == CombatTeam.Player ? CombatTeam.Enemy : CombatTeam.Player;
            CurrentTarget = manager.Registry.GetNearestAlive(transform.position, enemyTeam);
        }
    }
}
