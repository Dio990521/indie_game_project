using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 武器充能组件：
    /// 充能来源 = 普攻命中（AddCharge，由 AutoAttackController/Projectile 调用）
    ///          + 每秒被动充能（武器速率 + 角色 ChargeRate 属性加成）——与伤害数值无关。
    /// 充能满后由玩家按技能键消费（SkillCaster.TryCast）。
    /// UnitChargeChangedEvent 按整数百分比节流广播，避免逐帧刷 UI。
    /// 本组件是 StatType.ChargeRate 占位属性的首个消费者。
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponCharge : MonoBehaviour
    {
        /// <summary> 当前充能值 </summary>
        public float Current { get; private set; }

        /// <summary> 充能上限 </summary>
        public float Max { get; private set; } = 100f;

        /// <summary> 是否已充满 </summary>
        public bool IsFull => Current >= Max;

        private CombatUnit _self;
        // 广播节流：上次广播的整数百分比
        private int _lastBroadcastPercent = -1;

        /// <summary>
        /// 初始化（由 CombatUnit.Initialize 调用）：充能清零并广播一次初始值。
        /// </summary>
        public void Initialize(CombatUnit self, WeaponCombatDataSO weapon)
        {
            _self = self;
            Max = weapon != null ? Mathf.Max(1f, weapon.MaxCharge) : 100f;
            Current = 0f;
            _lastBroadcastPercent = -1;
            Broadcast();
        }

        private void Update()
        {
            CombatManager manager = CombatManager.Instance;
            if (manager == null || !manager.BattleRunning) return;
            if (_self == null || !_self.IsAlive || _self.WeaponData == null) return;
            if (IsFull) return;

            // 每秒被动充能：武器自身速率 + 角色 ChargeRate 加成
            float statRate = _self.Stats != null ? _self.Stats.ChargeRate.Value : 0f;
            float perSecond = CombatFormulas.CalculatePassiveChargePerSecond(
                _self.WeaponData.ChargePerSecond, statRate);
            if (perSecond <= 0f) return;
            AddCharge(perSecond * Time.deltaTime);
        }

        /// <summary>
        /// 增加充能（普攻命中/被动累积共用入口）。
        /// </summary>
        public void AddCharge(float amount)
        {
            if (amount <= 0f || IsFull) return;
            Current = Mathf.Min(Max, Current + amount);
            Broadcast();
        }

        /// <summary>
        /// 清空充能（技能释放后调用）。
        /// </summary>
        public void ResetCharge()
        {
            Current = 0f;
            Broadcast();
        }

        /// <summary>
        /// 节流广播：整数百分比变化才发事件。
        /// </summary>
        private void Broadcast()
        {
            int percent = Max > 0f ? Mathf.FloorToInt(Current / Max * 100f) : 0;
            if (percent == _lastBroadcastPercent) return;
            _lastBroadcastPercent = percent;
            EventBus.Raise(new UnitChargeChangedEvent
            {
                Owner = gameObject,
                Current = Current,
                Max = Max
            });
        }
    }
}
