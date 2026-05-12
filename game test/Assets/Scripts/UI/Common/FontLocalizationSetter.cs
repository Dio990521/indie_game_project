using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace IndieGame.UI.Common
{
    /// <summary>
    /// 字体本地化组件：监听语言切换事件，自动把当前 GameObject（及可选子对象）
    /// 上的所有 TMP_Text 字体替换为对应语言的字体资产。
    ///
    /// 使用方式：
    ///   1. 在 Localization Asset Table 中为每种语言配置一条记录（Key 自定义，Value = TMP_FontAsset）。
    ///   2. 将此组件挂在 UI 根节点或单个 TMP_Text 对象上。
    ///   3. 在 Inspector 中填写 fontReference（指向 Asset Table 的记录）。
    ///   4. 勾选 applyToChildren 可批量应用到所有子 TMP_Text。
    /// </summary>
    public class FontLocalizationSetter : MonoBehaviour
    {
        [Tooltip("Asset Table 中对应字体资产的本地化引用")]
        [SerializeField] private LocalizedAsset<TMP_FontAsset> fontReference;

        [Tooltip("是否递归替换所有子 TMP_Text 的字体，关闭则只替换本对象上的 TMP_Text")]
        [SerializeField] private bool applyToChildren = true;

        private void OnEnable()
        {
            // 语言切换时重新加载字体
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            ApplyFont();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale _) => ApplyFont();

        /// <summary>
        /// 异步加载当前语言的字体并写入所有目标 TMP_Text。
        /// </summary>
        private void ApplyFont()
        {
            if (fontReference == null || fontReference.IsEmpty) return;

            var handle = fontReference.LoadAssetAsync();
            handle.Completed += op =>
            {
                // 异步回调到达时组件可能已被销毁
                if (this == null) return;

                TMP_FontAsset font = op.Result;
                if (font == null) return;

                if (applyToChildren)
                {
                    // 批量替换本对象及所有子对象上的 TMP_Text
                    TMP_Text[] targets = GetComponentsInChildren<TMP_Text>(includeInactive: true);
                    foreach (TMP_Text t in targets)
                        t.font = font;
                }
                else
                {
                    // 只替换本对象上的 TMP_Text
                    if (TryGetComponent(out TMP_Text text))
                        text.font = font;
                }
            };
        }
    }
}
