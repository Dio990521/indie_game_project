using IndieGame.Core;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 道具瞄准状态（Overlay，继承选点瞄准骨架）：
    /// 需瞄准的战斗道具按数字键进入本状态，选点后再按同一数字键确认使用。
    /// 指向解析/NavMesh 校验/指示器驱动由 AimingOverlayStateBase 提供，
    /// 本类只负责道具业务——确认时经 CombatManager 消耗并执行道具效果。
    /// </summary>
    public class ItemAimingState : AimingOverlayStateBase
    {
        /// <summary> 进入瞄准时的道具栏槽位索引（确认键匹配用） </summary>
        public int SlotIndex { get; }

        /// <summary> 正在瞄准的道具 </summary>
        public CombatItemSO Item { get; }

        public ItemAimingState(int slotIndex, CombatItemSO item)
        {
            SlotIndex = slotIndex;
            Item = item;
        }

        protected override float GetAimRadius(CombatManager context)
        {
            return Item != null ? Item.CastRange : 8f;
        }

        protected override void OnAimingEntered(CombatManager context)
        {
            EventBus.Raise(new ItemAimStartedEvent { SlotIndex = SlotIndex, Item = Item });
        }

        /// <summary>
        /// 确认使用：落点合法则消耗道具并执行效果；非法则提示。
        /// </summary>
        public override void Confirm(CombatManager context)
        {
            if (!IsValidPoint)
            {
                EventBus.Raise(new ItemUseRejectedEvent { SlotIndex = SlotIndex });
                return;
            }

            context.UseItem(SlotIndex, Item, SnappedPoint);
            context.PopOverlayState();
            EventBus.Raise(new ItemAimEndedEvent { Confirmed = true });
        }

        /// <summary>
        /// 取消瞄准（不消耗道具）。
        /// </summary>
        public override void Cancel(CombatManager context)
        {
            context.PopOverlayState();
            EventBus.Raise(new ItemAimEndedEvent { Confirmed = false });
        }
    }
}
