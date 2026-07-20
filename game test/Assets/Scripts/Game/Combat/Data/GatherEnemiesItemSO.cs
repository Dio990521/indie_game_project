using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 聚怪道具：
    /// 把落点周围 PullRadius 内的敌方存活单位强制拉拢到落点附近，
    /// 便于配合范围技能集中收割。位移走 NavMeshAgent.Warp（落点环形散布 + NavMesh 吸附），
    /// 目标筛选走 CombatUnitRegistry，零物理查询。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Combat/Item/Gather Enemies")]
    public class GatherEnemiesItemSO : CombatItemSO
    {
        [Header("聚怪")]
        [Tooltip("受影响敌人的搜索半径（以落点为圆心）")]
        public float PullRadius = 6f;

        [Tooltip("被拉拢后敌人环绕落点散布的半径（避免完全重叠）")]
        public float ScatterRadius = 1.2f;

        // 目标筛选复用缓冲（主线程按键触发，静态共享安全）
        private static readonly List<CombatUnit> _targetBuffer = new List<CombatUnit>(8);

        public override void Execute(CombatManager manager, Vector3 point)
        {
            if (manager == null) return;

            manager.Registry.GetAliveUnitsNonAlloc(CombatTeam.Enemy, _targetBuffer);
            if (_targetBuffer.Count == 0) return;

            float sqrPullRadius = PullRadius * PullRadius;
            int pulled = 0;
            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                CombatUnit unit = _targetBuffer[i];
                Vector3 offset = unit.transform.position - point;
                offset.y = 0f;
                if (offset.sqrMagnitude > sqrPullRadius) continue;

                // 环绕落点均匀散布（按已拉拢数递增角度），NavMesh 吸附保证落点合法
                float angle = pulled * 137.5f * Mathf.Deg2Rad; // 黄金角散布，避免规则排列重叠
                Vector3 target = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ScatterRadius;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    target = hit.position;
                }

                if (unit.Mover == null || !unit.Mover.WarpTo(target))
                {
                    unit.transform.position = target;
                }
                pulled++;
            }

            DebugTools.Log($"<color=cyan>[Combat] 聚怪道具生效：拉拢 {pulled} 个敌人到落点。</color>");
        }
    }
}
