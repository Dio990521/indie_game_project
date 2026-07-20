using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Gameplay.Combat;

namespace IndieGame.UI.Combat
{
    /// <summary>
    /// 战斗道具槽 UI（纯显示，不可点击——道具操作走数字键 1-4/手柄十字键）：
    /// 显示道具图标、持有数量与按键提示；瞄准态时高亮本槽。
    /// 不直接订阅 EventBus——由 CombatHudController 统一分发刷新调用（单一职责）。
    /// </summary>
    public class CombatItemSlotUI : MonoBehaviour
    {
        [Header("显示元素")]
        [Tooltip("道具图标")]
        [SerializeField] private Image iconImage;

        [Tooltip("持有数量文本")]
        [SerializeField] private TMP_Text countLabel;

        [Tooltip("按键提示文本（1-4）")]
        [SerializeField] private TMP_Text keyHintLabel;

        [Tooltip("空槽遮罩（无道具时显示）")]
        [SerializeField] private GameObject emptyOverlay;

        [Tooltip("瞄准态高亮（该槽道具正在选点时显示）")]
        [SerializeField] private GameObject aimingHighlight;

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
        /// 设置按键提示文案（构建槽位时一次性设置）。
        /// </summary>
        public void SetKeyHint(string hint)
        {
            if (keyHintLabel != null) keyHintLabel.text = hint;
        }

        /// <summary>
        /// 显示某种道具及其数量。
        /// </summary>
        public void SetStack(CombatItemSO item, int count)
        {
            if (iconImage != null)
            {
                iconImage.sprite = item != null ? item.Icon : null;
                iconImage.enabled = item != null && item.Icon != null;
            }
            if (countLabel != null)
            {
                countLabel.gameObject.SetActive(true);
                countLabel.text = count.ToString();
            }
            if (emptyOverlay != null) emptyOverlay.SetActive(false);
        }

        /// <summary>
        /// 置为空槽显示。
        /// </summary>
        public void SetEmpty()
        {
            if (iconImage != null) iconImage.enabled = false;
            if (countLabel != null) countLabel.gameObject.SetActive(false);
            if (emptyOverlay != null) emptyOverlay.SetActive(true);
            SetAiming(false);
        }

        /// <summary>
        /// 设置瞄准态高亮。
        /// </summary>
        public void SetAiming(bool aiming)
        {
            if (aimingHighlight != null) aimingHighlight.SetActive(aiming);
        }

        /// <summary>
        /// 无效操作抖动提示（空槽/条件不满足/落点非法）。
        /// </summary>
        public void PlayRejectShake()
        {
            if (_rectTransform == null) return;
            _shakeTween?.Kill(true);
            _shakeTween = _rectTransform.DOShakeAnchorPos(0.3f, 8f, 20);
        }
    }
}
