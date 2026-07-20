using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using IndieGame.Gameplay.Combat;
using IndieGame.UI.Common;

namespace IndieGame.UI.Combat
{
    /// <summary>
    /// 战斗 HUD 视图（MVB 的 View：渲染与显隐，不做业务判断）：
    /// 名册槽位构建、选中态分发、结算横幅与放置提示。
    /// 选择高亮跟随槽位自身（RosterSlotUI.SetSelected），不再是 HUD 上单独移动的指针。
    /// 业务事件的分发由 CombatHudController 完成。
    /// </summary>
    public class CombatHudView : MonoBehaviour
    {
        [Tooltip("绑定器（同物体或子物体上的引用容器）")]
        [SerializeField] private CombatHudBinder binder;

        // 已生成的槽位实例（最多 5 个，战斗间复用不销毁）
        private readonly List<RosterSlotUI> _slots = new List<RosterSlotUI>(CombatRoster.MaxRosterSize);
        // 道具槽实例（固定 4 个，一次性构建后复用）
        private readonly List<CombatItemSlotUI> _itemSlots = new List<CombatItemSlotUI>(CombatItemBar.MaxSlots);
        // 显隐动画引用
        private Sequence _fadeSequence;
        // 当前选中槽位索引（-1 = 无），切换选中时先关旧的再开新的
        private int _selectedIndex = -1;
        // 当前瞄准中的道具槽索引（-1 = 无）
        private int _aimingItemIndex = -1;

        /// <summary> 当前槽位列表（Controller 按索引/成员刷新用） </summary>
        public IReadOnlyList<RosterSlotUI> Slots => _slots;

        private void Awake()
        {
            // 初始隐藏（进入战斗模式后由 Controller 调 Show）
            if (binder != null && binder.RootCanvasGroup != null)
            {
                binder.RootCanvasGroup.alpha = 0f;
                binder.RootCanvasGroup.blocksRaycasts = false;
                binder.RootCanvasGroup.interactable = false;
            }
            SetResultVisible(false, true);
            SetPlacementHintVisible(false);
        }

        private void OnDestroy()
        {
            _fadeSequence?.Kill();
        }

        /// <summary>
        /// 显示 HUD（淡入）。
        /// </summary>
        public void Show()
        {
            if (binder == null || binder.RootCanvasGroup == null) return;
            _fadeSequence?.Kill();
            _fadeSequence = UIAnimationHelper.PlayFadeIn(binder.RootCanvasGroup);
        }

        /// <summary>
        /// 隐藏 HUD（淡出）并复位结算/提示。
        /// </summary>
        public void Hide()
        {
            if (binder == null || binder.RootCanvasGroup == null) return;
            _fadeSequence?.Kill();
            _fadeSequence = UIAnimationHelper.PlayFadeOut(binder.RootCanvasGroup);
            SetResultVisible(false, true);
            SetPlacementHintVisible(false);
        }

        /// <summary>
        /// 按名册重建槽位（数量不足则实例化补齐，多余的隐藏；槽位实例跨战斗复用）。
        /// </summary>
        public void BuildSlots(CombatRoster roster)
        {
            if (binder == null || binder.RosterSlotPrefab == null || binder.RosterSlotContainer == null) return;

            int needed = roster != null ? roster.Members.Count : 0;

            // 补齐实例
            while (_slots.Count < needed)
            {
                RosterSlotUI slot = Instantiate(binder.RosterSlotPrefab, binder.RosterSlotContainer);
                _slots.Add(slot);
            }

            // 绑定/隐藏（Bind 内部会把选中态重置为 false，调用方需在 BuildSlots 后调用
            // SetSelectedIndex 重新点亮当前选中槽位）
            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < needed;
                _slots[i].gameObject.SetActive(used);
                if (used) _slots[i].Bind(roster.Members[i]);
            }
            _selectedIndex = -1;
        }

        /// <summary>
        /// 按成员查槽位（Controller 分发事件时定位用）。
        /// </summary>
        public RosterSlotUI FindSlot(RosterMember member)
        {
            if (member == null) return null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].BoundMember == member) return _slots[i];
            }
            return null;
        }

        /// <summary>
        /// 构建/刷新道具栏（固定 4 个种类槽；按道具栏当前内容显示，空槽置灰）。
        /// </summary>
        public void RefreshItemBar(CombatItemBar bar)
        {
            if (binder == null || binder.ItemSlotPrefab == null || binder.ItemSlotContainer == null) return;

            // 一次性补齐固定数量的槽位实例
            while (_itemSlots.Count < CombatItemBar.MaxSlots)
            {
                CombatItemSlotUI slot = Instantiate(binder.ItemSlotPrefab, binder.ItemSlotContainer);
                slot.SetKeyHint((_itemSlots.Count + 1).ToString());
                _itemSlots.Add(slot);
            }

            for (int i = 0; i < _itemSlots.Count; i++)
            {
                if (bar != null && i < bar.Stacks.Count)
                {
                    _itemSlots[i].SetStack(bar.Stacks[i].Item, bar.Stacks[i].Count);
                }
                else
                {
                    _itemSlots[i].SetEmpty();
                }
            }

            // 内容变化后重新应用瞄准高亮（槽位可能位移）
            if (_aimingItemIndex >= 0) SetItemAiming(_aimingItemIndex, true);
        }

        /// <summary>
        /// 设置道具槽瞄准态高亮（同一时刻至多一个）。
        /// </summary>
        public void SetItemAiming(int index, bool aiming)
        {
            if (_aimingItemIndex >= 0 && _aimingItemIndex < _itemSlots.Count)
            {
                _itemSlots[_aimingItemIndex].SetAiming(false);
            }

            _aimingItemIndex = aiming ? index : -1;
            if (aiming && index >= 0 && index < _itemSlots.Count)
            {
                _itemSlots[index].SetAiming(true);
            }
        }

        /// <summary>
        /// 播放道具槽拒绝抖动。
        /// </summary>
        public void ShakeItemSlot(int index)
        {
            if (index >= 0 && index < _itemSlots.Count)
            {
                _itemSlots[index].PlayRejectShake();
            }
        }

        /// <summary>
        /// 切换选中槽位：熄灭旧槽位的高亮、点亮新槽位的高亮（高亮跟随槽位自身，无需再做位置动画）。
        /// </summary>
        public void SetSelectedIndex(int index)
        {
            if (_selectedIndex >= 0 && _selectedIndex < _slots.Count)
            {
                _slots[_selectedIndex].SetSelected(false);
            }

            _selectedIndex = index;
            if (index < 0 || index >= _slots.Count || !_slots[index].gameObject.activeSelf) return;
            _slots[index].SetSelected(true);
        }

        /// <summary>
        /// 显示/隐藏结算横幅。
        /// </summary>
        public void SetResultVisible(bool visible, bool instant = false)
        {
            if (binder == null || binder.ResultPanel == null) return;
            binder.ResultPanel.SetActive(visible);
            if (!visible || instant) return;
        }

        /// <summary>
        /// 设置结算文案（胜利/失败）。
        /// </summary>
        public void ShowResult(bool victory)
        {
            SetResultVisible(true);
            if (binder != null && binder.ResultText != null)
            {
                binder.ResultText.text = victory ? "胜利！" : "战败…";
            }
        }

        /// <summary>
        /// 显示/隐藏放置态操作提示。
        /// </summary>
        public void SetPlacementHintVisible(bool visible)
        {
            if (binder == null || binder.PlacementHintText == null) return;
            binder.PlacementHintText.gameObject.SetActive(visible);
        }
    }
}
