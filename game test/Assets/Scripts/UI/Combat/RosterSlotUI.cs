using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Gameplay.Combat;

namespace IndieGame.UI.Combat
{
    /// <summary>
    /// 名册槽位 UI（纯显示，不可点击——战斗操作主体是按键，避免鼠标误触）：
    /// 显示头像、HP 微条、充能条、重上场冷却圈与状态角标（在场/后台/阵亡）。
    /// 不直接订阅 EventBus——由 CombatHudController 统一分发刷新调用（单一职责）。
    /// </summary>
    public class RosterSlotUI : MonoBehaviour
    {
        [Header("显示元素")]
        [Tooltip("角色头像")]
        [SerializeField] private Image portraitImage;

        [Tooltip("角色名文本")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("HP 微条（fillAmount）")]
        [SerializeField] private Image hpFill;

        [Tooltip("武器充能条（fillAmount）")]
        [SerializeField] private Image chargeFill;

        [Tooltip("重上场冷却遮罩（Radial fillAmount，0 = 无冷却）")]
        [SerializeField] private Image cooldownOverlay;

        [Tooltip("冷却剩余秒数文本（可空）")]
        [SerializeField] private TMP_Text cooldownLabel;

        [Tooltip("阵亡灰化遮罩")]
        [SerializeField] private GameObject deadOverlay;

        [Tooltip("在场状态角标（区分在场/后台）")]
        [SerializeField] private GameObject onFieldBadge;

        [Tooltip("充能就绪发光（充能满时显示）")]
        [SerializeField] private GameObject chargeReadyGlow;

        /// <summary> 本槽位绑定的名册成员 </summary>
        public RosterMember BoundMember { get; private set; }

        // 抖动动画引用（重复触发时先 Kill）
        private Tween _shakeTween;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
        }

        private void OnDestroy()
        {
            _shakeTween?.Kill();
        }

        /// <summary>
        /// 绑定名册成员并做一次全量刷新。
        /// </summary>
        public void Bind(RosterMember member)
        {
            BoundMember = member;
            if (member == null || member.Definition == null) return;

            if (portraitImage != null)
            {
                portraitImage.sprite = member.Definition.Portrait;
                portraitImage.enabled = member.Definition.Portrait != null;
            }
            if (nameLabel != null)
            {
                string displayName = string.IsNullOrWhiteSpace(member.Definition.DisplayName)
                    ? member.Definition.name
                    : member.Definition.DisplayName;
                nameLabel.text = displayName;
            }

            SetHpPercent(1f);
            SetChargePercent(0f);
            SetCooldown(0f, 1f);
            RefreshState();
        }

        /// <summary>
        /// 按成员当前状态刷新角标/灰化（上下场、死亡后调用）。
        /// </summary>
        public void RefreshState()
        {
            if (BoundMember == null) return;
            bool isDead = BoundMember.State == RosterMemberState.Dead;
            bool onField = BoundMember.State == RosterMemberState.Field;

            if (deadOverlay != null) deadOverlay.SetActive(isDead);
            if (onFieldBadge != null) onFieldBadge.SetActive(onField);
            // 不在场/阵亡时不显示充能发光
            if (!onField && chargeReadyGlow != null) chargeReadyGlow.SetActive(false);
        }

        /// <summary>
        /// 刷新 HP 比例（0~1）。
        /// </summary>
        public void SetHpPercent(float percent)
        {
            if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(percent);
        }

        /// <summary>
        /// 刷新充能比例（0~1），满时点亮就绪发光。
        /// </summary>
        public void SetChargePercent(float percent)
        {
            float clamped = Mathf.Clamp01(percent);
            if (chargeFill != null) chargeFill.fillAmount = clamped;
            if (chargeReadyGlow != null)
            {
                chargeReadyGlow.SetActive(clamped >= 1f && BoundMember != null
                    && BoundMember.State == RosterMemberState.Field);
            }
        }

        /// <summary>
        /// 刷新重上场冷却（remaining=0 时隐藏冷却圈）。
        /// </summary>
        public void SetCooldown(float remaining, float total)
        {
            bool inCooldown = remaining > 0f;
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(inCooldown);
                cooldownOverlay.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            }
            if (cooldownLabel != null)
            {
                cooldownLabel.gameObject.SetActive(inCooldown);
                if (inCooldown) cooldownLabel.text = Mathf.CeilToInt(remaining).ToString();
            }
        }

        /// <summary>
        /// 无效操作抖动提示（技能未充满/冷却未结束等）。
        /// </summary>
        public void PlayRejectShake()
        {
            if (_rectTransform == null) return;
            _shakeTween?.Kill(true);
            _shakeTween = _rectTransform.DOShakeAnchorPos(0.3f, 8f, 20);
        }
    }
}
