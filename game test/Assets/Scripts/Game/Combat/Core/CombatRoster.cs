using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 名册成员的运行时状态：
    /// 定义（静态 SO）+ 当前状态（在场/后台/阵亡）+ 在场战斗体引用 + 重上场冷却。
    /// </summary>
    public class RosterMember
    {
        // 静态定义
        public CharacterDefinitionSO Definition;
        // 当前状态
        public RosterMemberState State = RosterMemberState.Backline;
        // 在场战斗体（State == Field 时有效）
        public CombatUnit FieldUnit;
        // 重上场就绪时刻（Time.time 基准；<= 当前时间表示冷却已结束）
        public float RedeployReadyTime;
        // 冷却广播节流：上次广播的整秒值（-1 = 未在冷却广播中）
        public int LastBroadcastWholeSecond = -1;
        // 下场时记录的剩余血量（-1 = 未记录，首次上场满血）：
        // 防止"回收再上场"变成无成本的回血手段
        public int SavedFieldHP = -1;

        /// <summary> 是否主角（固定 0 号槽、不可下场、死亡即战败） </summary>
        public bool IsProtagonist => Definition != null && Definition.IsProtagonist;

        /// <summary> 剩余重上场冷却秒数（0 = 就绪） </summary>
        public float GetCooldownRemaining() => Mathf.Max(0f, RedeployReadyTime - Time.time);
    }

    /// <summary>
    /// 战斗名册（纯 C#，由 CombatManager 持有）：
    /// 管理最多 5 名可操控角色的三态迁移（Field/Backline/Dead）、
    /// 选择指针与上下场规则校验。场上人数上限 3（含主角）。
    /// 规则校验（CanDeploy/CanRecall/CanCastSkill）与状态迁移分离，便于单元测试。
    /// </summary>
    public class CombatRoster
    {
        /// <summary> 场上我方单位上限（含主角） </summary>
        public const int MaxFieldCount = 3;
        /// <summary> 名册人数上限 </summary>
        public const int MaxRosterSize = 5;

        private readonly List<RosterMember> _members = new List<RosterMember>(MaxRosterSize);

        /// <summary> 名册成员（顺序即 HUD 槽位顺序，0 号固定为主角） </summary>
        public IReadOnlyList<RosterMember> Members => _members;

        /// <summary> 当前选择指针指向的槽位索引 </summary>
        public int SelectedIndex { get; private set; }

        /// <summary> 当前选中的成员（名册为空时为 null） </summary>
        public RosterMember SelectedMember =>
            _members.Count > 0 && SelectedIndex >= 0 && SelectedIndex < _members.Count
                ? _members[SelectedIndex]
                : null;

        /// <summary> 主角成员（Build 后必定存在于 0 号槽） </summary>
        public RosterMember Protagonist => _members.Count > 0 ? _members[0] : null;

        /// <summary> 场上人数 </summary>
        public int FieldCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].State == RosterMemberState.Field) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 用角色定义列表构建名册：
        /// 主角强制排到 0 号槽，超出人数上限的定义被忽略并告警。
        /// 初始全员 Backline（主角的上场由 CombatInitState 显式执行）。
        /// </summary>
        public void Build(IReadOnlyList<CharacterDefinitionSO> definitions)
        {
            _members.Clear();
            SelectedIndex = 0;
            if (definitions == null) return;

            for (int i = 0; i < definitions.Count; i++)
            {
                CharacterDefinitionSO def = definitions[i];
                if (def == null) continue;
                if (_members.Count >= MaxRosterSize)
                {
                    DebugTools.LogWarning($"[CombatRoster] 名册已达上限 {MaxRosterSize}，忽略多余角色 {def.name}。");
                    break;
                }

                var member = new RosterMember { Definition = def, State = RosterMemberState.Backline };
                if (def.IsProtagonist && _members.Count > 0 && !_members[0].IsProtagonist)
                {
                    // 主角固定 0 号槽
                    _members.Insert(0, member);
                }
                else
                {
                    _members.Add(member);
                }
            }

            if (_members.Count == 0 || !_members[0].IsProtagonist)
            {
                DebugTools.LogError("[CombatRoster] 名册中缺少主角（IsProtagonist=true）——战斗无法正常进行。");
            }
        }

        /// <summary>
        /// 选择指针循环切换：Direction = +1 下一个 / -1 上一个。
        /// 切换成功后广播 RosterSelectionChangedEvent。
        /// </summary>
        public void MoveSelection(int direction)
        {
            if (_members.Count <= 1 || direction == 0) return;
            int step = direction > 0 ? 1 : -1;
            SelectedIndex = (SelectedIndex + step + _members.Count) % _members.Count;
            RaiseSelectionChanged();
        }

        /// <summary>
        /// 直接选中某个槽位（HUD 初始化/直选键用）。
        /// </summary>
        public void Select(int index)
        {
            if (index < 0 || index >= _members.Count) return;
            SelectedIndex = index;
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            EventBus.Raise(new RosterSelectionChangedEvent
            {
                SelectedIndex = SelectedIndex,
                Member = SelectedMember
            });
        }

        // ===================== 规则校验 =====================

        /// <summary>
        /// 是否允许该成员上场（后台、存活、冷却就绪、场上未满）。
        /// </summary>
        public bool CanDeploy(RosterMember member, out DeployRejectReason reason)
        {
            reason = default;
            if (member == null) return false;
            if (member.State == RosterMemberState.Dead)
            {
                reason = DeployRejectReason.MemberDead;
                return false;
            }
            if (member.GetCooldownRemaining() > 0f)
            {
                reason = DeployRejectReason.CooldownNotReady;
                return false;
            }
            if (FieldCount >= MaxFieldCount)
            {
                reason = DeployRejectReason.FieldFull;
                return false;
            }
            return member.State == RosterMemberState.Backline;
        }

        /// <summary>
        /// 是否允许该成员下场（在场且非主角）。
        /// </summary>
        public bool CanRecall(RosterMember member, out DeployRejectReason reason)
        {
            reason = default;
            if (member == null || member.State != RosterMemberState.Field) return false;
            if (member.IsProtagonist)
            {
                reason = DeployRejectReason.ProtagonistCannotRecall;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 是否允许该成员释放技能（在场、存活、有技能且充能满）。
        /// </summary>
        public bool CanCastSkill(RosterMember member, out SkillCastRejectReason reason)
        {
            reason = default;
            if (member == null) return false;
            if (member.State == RosterMemberState.Dead)
            {
                reason = SkillCastRejectReason.MemberDead;
                return false;
            }
            if (member.State != RosterMemberState.Field || member.FieldUnit == null)
            {
                reason = SkillCastRejectReason.NotOnField;
                return false;
            }
            if (member.FieldUnit.Caster == null || !member.FieldUnit.Caster.HasSkill)
            {
                reason = SkillCastRejectReason.NoSkill;
                return false;
            }
            if (member.FieldUnit.Charge == null || !member.FieldUnit.Charge.IsFull)
            {
                reason = SkillCastRejectReason.ChargeNotFull;
                return false;
            }
            return true;
        }

        // ===================== 状态迁移 =====================

        /// <summary>
        /// 标记成员上场（由 CombatManager 在战斗体生成后调用）。
        /// </summary>
        public void MarkDeployed(RosterMember member, CombatUnit unit)
        {
            if (member == null) return;
            member.State = RosterMemberState.Field;
            member.FieldUnit = unit;
            member.LastBroadcastWholeSecond = -1;
        }

        /// <summary>
        /// 标记成员下场并启动重上场冷却。
        /// </summary>
        /// <param name="member">下场成员</param>
        /// <param name="savedHP">下场时的剩余血量（再上场时恢复，防止回收变成免费回血）</param>
        public void MarkRecalled(RosterMember member, int savedHP)
        {
            if (member == null) return;
            member.State = RosterMemberState.Backline;
            member.FieldUnit = null;
            member.SavedFieldHP = savedHP;
            float cooldown = member.Definition != null ? member.Definition.RedeployCooldown : 0f;
            member.RedeployReadyTime = Time.time + Mathf.Max(0f, cooldown);
            member.LastBroadcastWholeSecond = -1;
        }

        /// <summary>
        /// 标记成员阵亡（本场不可再上场）。
        /// </summary>
        public void MarkDead(RosterMember member)
        {
            if (member == null) return;
            member.State = RosterMemberState.Dead;
            member.FieldUnit = null;
        }

        /// <summary>
        /// 按在场战斗体反查名册成员（死亡处理用）。
        /// </summary>
        public RosterMember FindByUnit(CombatUnit unit)
        {
            if (unit == null) return null;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].FieldUnit == unit) return _members[i];
            }
            return null;
        }

        /// <summary>
        /// 冷却广播节流（由 CombatManager 每帧驱动）：
        /// 剩余冷却进入新的整秒才广播一次 RedeployCooldownTickEvent，
        /// 冷却归零时补发一次 Remaining=0 收尾。
        /// </summary>
        public void TickCooldownBroadcast()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                RosterMember member = _members[i];
                if (member.State != RosterMemberState.Backline) continue;

                float remaining = member.GetCooldownRemaining();
                if (remaining <= 0f)
                {
                    if (member.LastBroadcastWholeSecond > 0)
                    {
                        // 冷却结束：补发一次归零事件让 HUD 清掉冷却圈
                        member.LastBroadcastWholeSecond = -1;
                        EventBus.Raise(new RedeployCooldownTickEvent { Member = member, Remaining = 0f });
                    }
                    continue;
                }

                int wholeSecond = Mathf.CeilToInt(remaining);
                if (wholeSecond == member.LastBroadcastWholeSecond) continue;
                member.LastBroadcastWholeSecond = wholeSecond;
                EventBus.Raise(new RedeployCooldownTickEvent { Member = member, Remaining = remaining });
            }
        }
    }
}
