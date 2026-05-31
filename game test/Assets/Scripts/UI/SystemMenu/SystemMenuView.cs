using DG.Tweening;
using IndieGame.UI.Common;
using UnityEngine;

namespace IndieGame.UI.SystemMenu
{
    /// <summary>
    /// 系统菜单视图层（View）：
    /// 只负责"如何显示"，不负责"何时显示/显示什么业务数据来源"。
    ///
    /// 设计边界：
    /// - CanvasGroup 淡入淡出动画（复用 UIAnimationHelper）；
    /// - 语言按钮高亮刷新；
    /// - 系统按钮显隐控制；
    /// - 不订阅 EventBus；
    /// - 不做业务判断。
    /// </summary>
    public class SystemMenuView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private SystemMenuBinder binder;

        private Sequence _panelTween;

        private void Awake()
        {
            // 初始状态：面板隐藏，按钮可见，Backdrop 关闭
            HidePanel(instant: true);
            SetButtonVisible(true, instant: true);
        }

        // ─── 面板显隐 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 淡入展示语言选项面板，同时激活 Backdrop 接收外部点击。
        /// </summary>
        public void ShowPanel()
        {
            if (binder == null || binder.PanelCanvasGroup == null) return;
            _panelTween?.Kill();
            SetBackdropActive(true);
            _panelTween = UIAnimationHelper.PlayFadeIn(
                binder.PanelCanvasGroup,
                binder.PanelRect,
                duration: 0.18f,
                startScale: 0.92f);
        }

        /// <summary>
        /// 淡出隐藏语言选项面板，同时关闭 Backdrop。
        /// </summary>
        public void HidePanel(bool instant = false)
        {
            SetBackdropActive(false);
            if (binder == null || binder.PanelCanvasGroup == null) return;
            _panelTween?.Kill();

            if (instant)
            {
                binder.PanelCanvasGroup.alpha          = 0f;
                binder.PanelCanvasGroup.blocksRaycasts = false;
                binder.PanelCanvasGroup.interactable   = false;
                if (binder.PanelRect != null)
                    binder.PanelRect.localScale = Vector3.one;
                return;
            }

            _panelTween = UIAnimationHelper.PlayFadeOut(
                binder.PanelCanvasGroup,
                binder.PanelRect,
                duration: 0.12f);
        }

        // ─── 系统按钮可见性 ───────────────────────────────────────────────────

        /// <summary>
        /// 控制右下角系统按钮的显示状态（对话/Loading 期间隐藏）。
        /// 使用 CanvasGroup 软隐藏，保持 GameObject 活跃以便 Controller 持续监听事件。
        /// </summary>
        public void SetButtonVisible(bool visible, bool instant = false)
        {
            if (binder == null || binder.ButtonCanvasGroup == null) return;

            binder.ButtonCanvasGroup.DOKill();

            if (instant)
            {
                binder.ButtonCanvasGroup.alpha          = visible ? 1f : 0f;
                binder.ButtonCanvasGroup.blocksRaycasts = visible;
                binder.ButtonCanvasGroup.interactable   = visible;
                return;
            }

            float target = visible ? 1f : 0f;
            binder.ButtonCanvasGroup
                .DOFade(target, 0.15f)
                .OnComplete(() =>
                {
                    if (binder == null || binder.ButtonCanvasGroup == null) return;
                    binder.ButtonCanvasGroup.blocksRaycasts = visible;
                    binder.ButtonCanvasGroup.interactable   = visible;
                });
        }

        // ─── 语言高亮 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 刷新四个语言按钮的高亮状态。
        /// selectedLocaleCode 传入 IETF 标签，如 "zh-Hans"、"en"、"ja"。
        /// </summary>
        public void RefreshLanguageHighlight(string selectedLocaleCode)
        {
            if (binder == null) return;
            SetButtonHighlight(binder.BtnZhHans, "zh-Hans", selectedLocaleCode);
            SetButtonHighlight(binder.BtnZhHant, "zh-Hant", selectedLocaleCode);
            SetButtonHighlight(binder.BtnEn,     "en",      selectedLocaleCode);
            SetButtonHighlight(binder.BtnJa,     "ja",      selectedLocaleCode);
        }

        private void SetButtonHighlight(UnityEngine.UI.Button btn, string localeCode, string selectedCode)
        {
            if (btn == null) return;
            var graphic = btn.targetGraphic;
            if (graphic == null) return;
            bool isSelected = string.Equals(localeCode, selectedCode,
                                            System.StringComparison.OrdinalIgnoreCase);
            graphic.color = isSelected ? binder.SelectedColor : binder.NormalColor;
        }

        // ─── Backdrop ────────────────────────────────────────────────────────

        private void SetBackdropActive(bool active)
        {
            if (binder == null || binder.BackdropButton == null) return;
            binder.BackdropButton.gameObject.SetActive(active);
        }
    }
}
