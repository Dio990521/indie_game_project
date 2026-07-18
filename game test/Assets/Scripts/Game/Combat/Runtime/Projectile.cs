using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 池化弹道：
    /// 追踪目标当前位置飞行，接近后结算伤害（按命中时目标防御计算）并回池。
    /// 目标中途死亡/战斗停止时直接回池。生命周期由 CombatManager 的弹道池管理。
    /// </summary>
    [DisallowMultipleComponent]
    public class Projectile : MonoBehaviour
    {
        [Tooltip("命中判定距离（米）")]
        [SerializeField] private float hitDistance = 0.5f;

        [Tooltip("瞄准目标身体的高度偏移（米），避免弹道贴地飞")]
        [SerializeField] private float targetHeightOffset = 0.8f;

        private CombatUnit _owner;
        private CombatUnit _target;
        // 出手时锁定的"基础伤害 + 攻击力"（防御在命中时按目标当前值扣减）
        private int _damageBeforeDefense;
        private float _speed;
        private bool _active;

        /// <summary>
        /// 发射（由 CombatManager.SpawnProjectile 调用）。
        /// </summary>
        public void Launch(CombatUnit owner, CombatUnit target, int damageBeforeDefense, float speed)
        {
            _owner = owner;
            _target = target;
            _damageBeforeDefense = damageBeforeDefense;
            _speed = Mathf.Max(0.1f, speed);
            _active = true;
        }

        private void Update()
        {
            if (!_active) return;

            CombatManager manager = CombatManager.Instance;
            if (manager == null || !manager.BattleRunning)
            {
                Despawn();
                return;
            }

            // 目标失效（死亡/下场回收）：弹道作废
            if (_target == null || !_target.IsAlive)
            {
                Despawn();
                return;
            }

            Vector3 targetPoint = _target.transform.position + Vector3.up * targetHeightOffset;
            Vector3 toTarget = targetPoint - transform.position;
            float step = _speed * Time.deltaTime;

            if (toTarget.sqrMagnitude <= Mathf.Max(hitDistance * hitDistance, step * step))
            {
                Impact();
                return;
            }

            // 朝目标当前位置追踪飞行
            Vector3 dir = toTarget.normalized;
            transform.position += dir * step;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>
        /// 命中结算：按目标当前防御计算伤害，并回调攻击方充能。
        /// </summary>
        private void Impact()
        {
            if (_target != null && _target.IsAlive && _target.Stats != null)
            {
                int defense = Mathf.RoundToInt(_target.Stats.Defense.Value);
                int damage = CombatFormulas.CalculateDamage(_damageBeforeDefense, 0, defense);
                _target.Stats.TakeDamage(damage);

                // 命中充能回调（攻击方可能已死亡，NotifyHit 内部自校验）
                if (_owner != null && _owner.Attack != null)
                {
                    _owner.Attack.NotifyHit();
                }
            }
            Despawn();
        }

        /// <summary>
        /// 回池（由 CombatManager 统一 Release）。
        /// </summary>
        private void Despawn()
        {
            _active = false;
            _owner = null;
            _target = null;
            CombatManager manager = CombatManager.Instance;
            if (manager != null)
            {
                manager.ReleaseProjectile(this);
            }
            else
            {
                // 战斗管理器已随场景销毁：直接销毁自身兜底
                Destroy(gameObject);
            }
        }
    }
}
