using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗单位注册表（纯 C#，由 CombatManager 持有）：
    /// 用列表扫描替代物理查询做索敌——场上单位 ≤ 8 个，
    /// sqrMagnitude 线性扫描零 GC、零物理开销（符合"Update 不做高频分配"的项目准则）。
    /// 同时维护 GameObject → CombatUnit 反查表，供 DeathEvent 等以 GameObject 为主体的事件定位单位。
    /// </summary>
    public class CombatUnitRegistry
    {
        // 按阵营分列表存储（在场单位，含存活与刚死亡未移除的瞬间）
        private readonly List<CombatUnit> _playerUnits = new List<CombatUnit>(4);
        private readonly List<CombatUnit> _enemyUnits = new List<CombatUnit>(8);
        // GameObject 反查表（DeathEvent.Owner / HealthChangedEvent.Owner 定位用）
        private readonly Dictionary<GameObject, CombatUnit> _unitByGameObject = new Dictionary<GameObject, CombatUnit>(12);

        /// <summary>
        /// 注册在场单位（由 CombatManager 在生成/上场时显式调用）。
        /// </summary>
        public void Register(CombatUnit unit)
        {
            if (unit == null) return;
            List<CombatUnit> list = GetTeamList(unit.Team);
            if (list.Contains(unit)) return;
            list.Add(unit);
            _unitByGameObject[unit.gameObject] = unit;
        }

        /// <summary>
        /// 注销单位（死亡/下场时调用）。
        /// </summary>
        public void Unregister(CombatUnit unit)
        {
            if (unit == null) return;
            GetTeamList(unit.Team).Remove(unit);
            _unitByGameObject.Remove(unit.gameObject);
        }

        /// <summary>
        /// 清空注册表（战斗结束/重开时调用）。
        /// </summary>
        public void Clear()
        {
            _playerUnits.Clear();
            _enemyUnits.Clear();
            _unitByGameObject.Clear();
        }

        /// <summary>
        /// 按 GameObject 反查战斗单位（未注册返回 null）。
        /// </summary>
        public CombatUnit FindByGameObject(GameObject go)
        {
            if (go == null) return null;
            return _unitByGameObject.TryGetValue(go, out CombatUnit unit) ? unit : null;
        }

        /// <summary>
        /// 获取距离 from 最近的指定阵营存活单位（索敌入口，零分配）。
        /// </summary>
        public CombatUnit GetNearestAlive(Vector3 from, CombatTeam team)
        {
            List<CombatUnit> list = GetTeamList(team);
            CombatUnit nearest = null;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                CombatUnit unit = list[i];
                if (unit == null || !unit.IsAlive) continue;
                float sqr = (unit.transform.position - from).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = unit;
                }
            }
            return nearest;
        }

        /// <summary>
        /// 统计指定阵营的存活单位数（胜负判定用）。
        /// </summary>
        public int CountAlive(CombatTeam team)
        {
            List<CombatUnit> list = GetTeamList(team);
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].IsAlive) count++;
            }
            return count;
        }

        /// <summary>
        /// 把指定阵营的存活单位填入调用方提供的缓冲列表（技能 AoE 遍历用，NonAlloc 风格）。
        /// </summary>
        public void GetAliveUnitsNonAlloc(CombatTeam team, List<CombatUnit> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            List<CombatUnit> list = GetTeamList(team);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].IsAlive) buffer.Add(list[i]);
            }
        }

        private List<CombatUnit> GetTeamList(CombatTeam team)
        {
            return team == CombatTeam.Player ? _playerUnits : _enemyUnits;
        }
    }
}
