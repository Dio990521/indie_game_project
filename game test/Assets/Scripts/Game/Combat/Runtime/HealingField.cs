using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 治疗领域（池化效果，挂在治疗道具的领域预制体上）：
    /// 持续期间按跳血间隔为范围内的我方存活单位回血（走 CombatUnitRegistry，零物理查询）。
    /// 到时/战斗停止后自动经 CombatManager 回池。
    /// </summary>
    [DisallowMultipleComponent]
    public class HealingField : MonoBehaviour
    {
        [Tooltip("视觉件（缩放匹配治疗半径的贴地圆盘，可空）")]
        [SerializeField] private Transform visual;

        // 目标筛选复用缓冲（同一时刻只在主线程 Update 中使用，静态共享安全）
        private static readonly List<CombatUnit> _targetBuffer = new List<CombatUnit>(4);

        private float _radius;
        private int _healPerTick;
        private float _tickInterval;
        private float _endTime;
        private float _nextTickTime;
        private bool _active;

        /// <summary>
        /// 启动领域（由 HealingFieldItemSO.Execute 在池取出后调用）。
        /// </summary>
        public void Begin(float radius, int healPerTick, float tickInterval, float duration)
        {
            _radius = Mathf.Max(0.5f, radius);
            _healPerTick = Mathf.Max(1, healPerTick);
            _tickInterval = Mathf.Max(0.1f, tickInterval);
            _endTime = Time.time + Mathf.Max(0.5f, duration);
            _nextTickTime = Time.time + _tickInterval;
            _active = true;

            // 视觉件缩放匹配半径（约定视觉件原始尺寸为直径 1 米的贴地圆盘）
            if (visual != null)
            {
                visual.localScale = new Vector3(_radius * 2f, 1f, _radius * 2f);
            }
        }

        private void Update()
        {
            if (!_active) return;

            CombatManager manager = CombatManager.Instance;
            if (manager == null || !manager.BattleRunning || Time.time >= _endTime)
            {
                Despawn(manager);
                return;
            }

            if (Time.time < _nextTickTime) return;
            _nextTickTime = Time.time + _tickInterval;

            // 一次跳血：治疗范围内的我方存活单位
            manager.Registry.GetAliveUnitsNonAlloc(CombatTeam.Player, _targetBuffer);
            float sqrRadius = _radius * _radius;
            for (int i = 0; i < _targetBuffer.Count; i++)
            {
                CombatUnit unit = _targetBuffer[i];
                Vector3 offset = unit.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > sqrRadius) continue;
                if (unit.Stats != null) unit.Stats.Heal(_healPerTick);
            }
        }

        private void Despawn(CombatManager manager)
        {
            _active = false;
            if (manager != null)
            {
                manager.ReleasePooledEffect(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
