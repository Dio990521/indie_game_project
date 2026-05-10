using DG.Tweening;
using UnityEngine;

namespace IndieGame.UI.Common
{
    /// <summary>
    /// 通用 UI 动画助手：
    /// 把多个 View 中重复出现的 CanvasGroup 淡入淡出 + 可选缩放动画
    /// 集中到这里，调用方只负责传入组件与参数。
    ///
    /// 注意：
    /// - 返回的是 Sequence，调用方需要自己 Kill 旧动画或保存引用；
    /// - Helper 不接管 alpha=0/1 之外的初始状态；
    /// - 复杂 stagger / 多元素时序动画建议保留各自实现，本工具不做覆盖。
    /// </summary>
    public static class UIAnimationHelper
    {
        /// <summary>
        /// 播放“淡入 + 可选缩放放大”动画。
        /// 调用方应在调用前 Kill 旧动画并设置初始 alpha=0、初始 scale=startScale。
        /// </summary>
        /// <param name="cg">目标 CanvasGroup（必填）。</param>
        /// <param name="rect">可选 RectTransform，传入则附加缩放动画。</param>
        /// <param name="duration">动画时长，默认 0.2s。</param>
        /// <param name="startScale">起始缩放，默认 0.9。</param>
        /// <param name="ease">缓动曲线，默认 OutCubic。</param>
        /// <returns>构建好的动画 Sequence；cg 为 null 时返回 null。</returns>
        public static Sequence PlayFadeIn(
            CanvasGroup cg,
            RectTransform rect = null,
            float duration = 0.2f,
            float startScale = 0.9f,
            Ease ease = Ease.OutCubic)
        {
            if (cg == null) return null;

            cg.alpha = 0f;
            cg.blocksRaycasts = true;
            cg.interactable = true;

            if (rect != null) rect.localScale = Vector3.one * startScale;

            Sequence seq = DOTween.Sequence();
            seq.Join(cg.DOFade(1f, duration));
            if (rect != null)
            {
                seq.Join(rect.DOScale(1f, duration).SetEase(ease));
            }
            return seq;
        }

        /// <summary>
        /// 播放“淡出 + 可选缩放收缩”动画，结束后关闭 CanvasGroup 的交互。
        /// </summary>
        /// <param name="cg">目标 CanvasGroup（必填）。</param>
        /// <param name="rect">可选 RectTransform。</param>
        /// <param name="duration">动画时长，默认 0.15s。</param>
        /// <param name="endScale">结束缩放，默认 0.95。</param>
        /// <param name="ease">缓动曲线，默认 InCubic。</param>
        /// <returns>构建好的动画 Sequence；cg 为 null 时返回 null。</returns>
        public static Sequence PlayFadeOut(
            CanvasGroup cg,
            RectTransform rect = null,
            float duration = 0.15f,
            float endScale = 0.95f,
            Ease ease = Ease.InCubic)
        {
            if (cg == null) return null;

            Sequence seq = DOTween.Sequence();
            seq.Join(cg.DOFade(0f, duration));
            if (rect != null)
            {
                seq.Join(rect.DOScale(endScale, duration).SetEase(ease));
            }
            seq.OnComplete(() =>
            {
                if (cg == null) return;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            });
            return seq;
        }
    }
}
