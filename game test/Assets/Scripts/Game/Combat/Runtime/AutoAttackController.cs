using UnityEngine;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 自动普攻组件：
    /// 有目标时——射程外驱动 Mover 追击；进入射程后停下、面向目标并按攻击间隔出手。
    /// 近战直接结算伤害；远程从 CombatManager 的弹道池发射 Projectile。
    /// 命中后为武器充能（"攻击频率"充能来源）。
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoAttackController : MonoBehaviour
    {
        [Tooltip("弹道发射点（可空 = 用单位位置抬高 1 米）")]
        [SerializeField] private Transform muzzle;

        private CombatUnit _self;
        private float _nextAttackTime;

        /// <summary>
        /// 初始化（由 CombatUnit.Initialize 调用）。
        /// </summary>
        public void Initialize(CombatUnit self)
        {
            _self = self;
            _nextAttackTime = 0f;
        }

        private void Update()
        {
            CombatManager manager = CombatManager.Instance;
            if (manager == null || !manager.BattleRunning) return;
            if (_self == null || !_self.IsAlive || _self.WeaponData == null) return;

            CombatUnit target = _self.CurrentTarget;
            if (target == null || !target.IsAlive)
            {
                // 无目标：原地待机
                if (_self.Mover != null) _self.Mover.Halt();
                return;
            }

            WeaponCombatDataSO weapon = _self.WeaponData;
            Vector3 offset = target.transform.position - transform.position;
            offset.y = 0f;
            float sqrDist = offset.sqrMagnitude;
            float range = Mathf.Max(0.5f, weapon.AttackRange);

            if (sqrDist > range * range)
            {
                // 射程外：追击（停止距离留一成余量，避免在射程边缘反复起停）
                if (_self.Mover != null) _self.Mover.ChaseTarget(target.transform, range * 0.9f);
                return;
            }

            // 射程内：停下、面向目标、按间隔出手
            if (_self.Mover != null)
            {
                _self.Mover.Halt();
                _self.Mover.FaceTarget(target.transform);
            }

            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + Mathf.Max(0.1f, weapon.AttackInterval);
            Fire(manager, weapon, target);
        }

        /// <summary>
        /// 出手一次：近战立即结算；远程发射池化弹道（伤害在命中时按目标防御结算）。
        /// </summary>
        private void Fire(CombatManager manager, WeaponCombatDataSO weapon, CombatUnit target)
        {
            int attack = Mathf.RoundToInt(_self.Stats != null ? _self.Stats.Attack.Value : 0f);

            if (weapon.IsRanged && weapon.ProjectilePrefab != null)
            {
                Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
                manager.SpawnProjectile(weapon.ProjectilePrefab, origin, _self, target,
                    weapon.BaseDamage + attack, weapon.ProjectileSpeed);
                return;
            }

            // 近战：直接结算并充能
            int defense = Mathf.RoundToInt(target.Stats != null ? target.Stats.Defense.Value : 0f);
            int damage = CombatFormulas.CalculateDamage(weapon.BaseDamage, attack, defense);
            target.Stats.TakeDamage(damage);
            NotifyHit();
        }

        /// <summary>
        /// 命中反馈：为武器充能（远程弹道命中时由 Projectile 回调）。
        /// </summary>
        public void NotifyHit()
        {
            if (_self == null || !_self.IsAlive) return;
            if (_self.Charge != null && _self.WeaponData != null)
            {
                _self.Charge.AddCharge(_self.WeaponData.ChargeGainPerHit);
            }
        }
    }
}
