using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using IndieGame.Gameplay.Combat;
using IndieGame.UI.Common;

namespace IndieGame.UI.Combat
{
    /// <summary>
    /// 战斗 HUD 视图（MVB 的 View：渲染与显隐，不做业务判断）：
    /// 名册槽位构建、选择指针移动、结算横幅与放置提示。
    /// 业务事件的分发由 CombatHudController 完成。
    /// </summary>
    public class CombatHudView : MonoBehaviour
    {
        [Tooltip("绑定器（同物体或子物体上的引用容器）")]
        [SerializeField] private CombatHudBinder binder;

        // 已生成的槽位实例（最多 5 个，战斗间复用不销毁）
        private readonly List<RosterSlotUI> _slots = new List<RosterSlotUI>(CombatRoster.MaxRosterSize);
        // 显隐动画引用
        private Sequence _fadeSequence;
        // 指针移动动画引用
        private Tween _cursorTween;

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
            _cursorTween?.Kill();
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

            // 绑定/隐藏
            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < needed;
                _slots[i].gameObject.SetActive(used);
                if (used) _slots[i].Bind(roster.Members[i]);
            }
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
        /// 移动选择指针到指定槽位上方（DOTween 平滑移动）。
        /// </summary>
        public void MoveSelectionCursor(int index)
        {
            if (binder == null || binder.SelectionCursor == null) return;
            if (index < 0 || index >= _slots.Count || !_slots[index].gameObject.activeSelf) return;

            binder.SelectionCursor.gameObject.SetActive(true);
            RectTransform slotRect = _slots[index].transform as RectTransform;
            if (slotRect == null) return;

            _cursorTween?.Kill();
            _cursorTween = binder.SelectionCursor
                .DOMove(slotRect.position, 0.12f)
                .SetEase(Ease.OutCubic);
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
