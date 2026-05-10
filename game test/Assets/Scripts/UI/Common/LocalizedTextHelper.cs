using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace IndieGame.UI.Common
{
    /// <summary>
    /// 本地化文本异步加载工具：
    /// 把“调用 GetLocalizedStringAsync + 完成后写入 TMP_Text + 校验目标对象有效性”这段
    /// 在多个 SlotUI 中重复出现的样板代码集中起来。
    ///
    /// 关键设计：
    /// - 异步回调到达时调用方可能已被销毁/重新绑定，因此需要做有效性校验；
    /// - 提供 guardObject 参数让调用方传一个“判定对象”，回调内通过 Unity 重载的
    ///   == null 判定该对象是否仍然有效；
    /// - 只负责“把异步结果写到 TMP_Text”，不替代调用方自身的业务校验
    ///   （如槽位绑定有没有变化），调用方仍可在外部校验后再调用。
    /// </summary>
    public static class LocalizedTextHelper
    {
        /// <summary>
        /// 异步把本地化文本写入 TMP_Text。
        /// </summary>
        /// <param name="text">目标文本组件。</param>
        /// <param name="source">本地化字符串。</param>
        /// <param name="guardObject">
        /// 可选护卫对象：异步回调到达时若该对象已被销毁则放弃写入。
        /// 一般传 text 自身即可；如调用方有自定义 UnityEngine.Object，可传该对象。
        /// </param>
        /// <param name="fallback">异步加载失败时的兜底文本（默认空字符串）。</param>
        public static void SetLocalizedAsync(
            TMP_Text text,
            LocalizedString source,
            Object guardObject = null,
            string fallback = "")
        {
            if (text == null) return;

            if (source == null)
            {
                text.text = fallback ?? string.Empty;
                return;
            }

            // 默认用 text 自身做护卫对象，避免对象销毁后再写值
            Object guard = guardObject != null ? guardObject : text;

            var handle = source.GetLocalizedStringAsync();
            handle.Completed += op =>
            {
                if (text == null) return;
                if (guard == null) return;
                text.text = op.Result;
            };
        }
    }
}
