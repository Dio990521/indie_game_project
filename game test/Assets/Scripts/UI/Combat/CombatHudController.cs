using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Combat;

namespace IndieGame.UI.Combat
{
    /// <summary>
    /// 战斗 HUD 控制器（MVB 的 Controller：订阅 EventBus 并驱动 View 刷新）：
    /// - 进入/离开 Combat 模式时显隐 HUD；
    /// - 战斗开始时按名册构建槽位；
    /// - 分发充能/血量/冷却/上下场/死亡事件到对应槽位；
    /// - 无效操作抖动提示与结算横幅。
    /// 操作不经过 UI——战斗输入事件由 CombatManager 直接消费，HUD 纯展示。
    /// </summary>
    public class CombatHudController : EventBusMonoBehaviour
    {
        [Tooltip("战斗 HUD 视图")]
        [SerializeField] private CombatHudView view;

        // 当前名册（战斗开始事件携带）
        private CombatRoster _roster;

        protected override void Bind()
        {
            Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
            Subscribe<CombatStartedEvent>(HandleCombatStarted);
            Subscribe<CombatEndedEvent>(HandleCombatEnded);
            Subscribe<RosterSelectionChangedEvent>(HandleSelectionChanged);
            Subscribe<UnitDeployedEvent>(HandleUnitDeployed);
            Subscribe<UnitRecalledEvent>(HandleUnitRecalled);
            Subscribe<CombatUnitDiedEvent>(HandleUnitDied);
            Subscribe<UnitChargeChangedEvent>(HandleChargeChanged);
            Subscribe<HealthChangedEvent>(HandleHealthChanged);
            Subscribe<RedeployCooldownTickEvent>(HandleCooldownTick);
            Subscribe<SkillCastRejectedEvent>(HandleSkillRejected);
            Subscribe<DeployRejectedEvent>(HandleDeployRejected);
            Subscribe<DeployPlacementStartedEvent>(HandlePlacementStarted);
            Subscribe<DeployPlacementEndedEvent>(HandlePlacementEnded);
        }

        /// <summary>
        /// 进入 Combat 模式显示 HUD，离开时隐藏（与 PlayerHud 的显隐惯例一致）。
        /// </summary>
        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            if (view == null) return;
            if (evt.Mode == GameMode.Combat)
            {
                view.Show();
            }
            else
            {
                _roster = null;
                view.Hide();
            }
        }

        private void HandleCombatStarted(CombatStartedEvent evt)
        {
            if (view == null) return;
            _roster = evt.Roster;
            view.SetResultVisible(false, true);
            view.BuildSlots(_roster);
            view.MoveSelectionCursor(_roster != null ? _roster.SelectedIndex : 0);
        }

        private void HandleCombatEnded(CombatEndedEvent evt)
        {
            if (view == null) return;
            view.SetPlacementHintVisible(false);
            view.ShowResult(evt.Victory);
        }

        private void HandleSelectionChanged(RosterSelectionChangedEvent evt)
        {
            if (view == null) return;
            view.MoveSelectionCursor(evt.SelectedIndex);
        }

        private void HandleUnitDeployed(UnitDeployedEvent evt)
        {
            RefreshSlotState(evt.Member);
        }

        private void HandleUnitRecalled(UnitRecalledEvent evt)
        {
            RefreshSlotState(evt.Member);
        }

        private void HandleUnitDied(CombatUnitDiedEvent evt)
        {
            // 敌人死亡与 HUD 无关；我方死亡时 FieldUnit 已被清空无法反查，
            // 直接全量刷新槽位状态（≤5 个，开销可忽略）
            if (evt.Unit == null || evt.Unit.Team != CombatTeam.Player) return;
            RefreshAllSlotStates();
        }

        /// <summary>
        /// 充能变化 → 定位归属成员的槽位刷新充能条。
        /// </summary>
        private void HandleChargeChanged(UnitChargeChangedEvent evt)
        {
            RosterSlotUI slot = FindSlotByOwner(evt.Owner);
            if (slot == null) return;
            slot.SetChargePercent(evt.Max > 0f ? evt.Current / evt.Max : 0f);
        }

        /// <summary>
        /// 血量变化 → 我方在场单位的槽位 HP 微条（敌人血量走头顶血条，不进 HUD）。
        /// </summary>
        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            RosterSlotUI slot = FindSlotByOwner(evt.Owner);
            if (slot == null) return;
            slot.SetHpPercent(evt.Max > 0 ? (float)evt.Current / evt.Max : 0f);
        }

        private void HandleCooldownTick(RedeployCooldownTickEvent evt)
        {
            if (view == null || evt.Member == null) return;
            RosterSlotUI slot = view.FindSlot(evt.Member);
            if (slot == null) return;
            float total = evt.Member.Definition != null ? evt.Member.Definition.RedeployCooldown : 1f;
            slot.SetCooldown(evt.Remaining, total);
        }

        private void HandleSkillRejected(SkillCastRejectedEvent evt)
        {
            ShakeSlot(evt.Member);
        }

        private void HandleDeployRejected(DeployRejectedEvent evt)
        {
            ShakeSlot(evt.Member);
        }

        private void HandlePlacementStarted(DeployPlacementStartedEvent evt)
        {
            if (view != null) view.SetPlacementHintVisible(true);
        }

        private void HandlePlacementEnded(DeployPlacementEndedEvent evt)
        {
            if (view != null) view.SetPlacementHintVisible(false);
        }

        // ===================== 内部工具 =====================

        /// <summary>
        /// 刷新成员槽位的状态角标（在场/后台/阵亡）。
        /// </summary>
        private void RefreshSlotState(RosterMember member)
        {
            if (view == null || member == null) return;
            RosterSlotUI slot = view.FindSlot(member);
            if (slot != null) slot.RefreshState();
        }

        private void ShakeSlot(RosterMember member)
        {
            if (view == null || member == null) return;
            RosterSlotUI slot = view.FindSlot(member);
            if (slot != null) slot.PlayRejectShake();
        }

        /// <summary>
        /// 全量刷新所有槽位的状态角标。
        /// </summary>
        private void RefreshAllSlotStates()
        {
            if (view == null) return;
            var slots = view.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].RefreshState();
            }
        }

        /// <summary>
        /// 按事件 Owner（战斗体 GameObject）定位对应的名册槽位；非我方在场单位返回 null。
        /// </summary>
        private RosterSlotUI FindSlotByOwner(GameObject owner)
        {
            if (view == null || _roster == null || owner == null) return null;
            var members = _roster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                RosterMember member = members[i];
                if (member.FieldUnit != null && member.FieldUnit.gameObject == owner)
                {
                    return view.FindSlot(member);
                }
            }
            return null;
        }
    }
}
