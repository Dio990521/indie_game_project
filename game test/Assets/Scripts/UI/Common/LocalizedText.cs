using IndieGame.Core.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace IndieGame.UI.Common
{
    /// <summary>
    /// 项目统一 UI 文字组件，整合两个本地化能力：
    ///   1. 字体跟随语言自动切换（向 GlobalFontManager 注册/注销）
    ///   2. 文字内容跟随语言自动刷新（监听 LocalizedString.StringChanged）
    ///
    /// 使用场景：
    ///   · 静态本地化文字 — Inspector 填写 Localized Key，零代码
    ///   · 动态赋值文字   — 不填 Key，直接赋 text，只享受字体自动切换
    ///   · 带参数的文字   — 填写 Key，运行时调用 SetArguments()
    /// </summary>
    public class LocalizedText : TextMeshProUGUI
    {
        [SerializeField] private LocalizedString localizedKey;

        // ─── 字体（由 GlobalFontManager 统一管理）────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            GlobalFontManager.Instance?.Register(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // 用 HasInstance 避免在销毁期触发 Find 查找导致虚假 Warning
            if (GlobalFontManager.HasInstance)
                GlobalFontManager.Instance.Unregister(this);
        }

        /// <summary>GlobalFontManager 内部调用，外部请勿直接使用。</summary>
        internal void ApplyFont(TMP_FontAsset targetFont)
        {
            font = targetFont;
        }

        // ─── 文字本地化（LocalizedString.StringChanged 自动驱动）────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!localizedKey.IsEmpty)
                localizedKey.StringChanged += OnStringChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!localizedKey.IsEmpty)
                localizedKey.StringChanged -= OnStringChanged;
        }

        private void OnStringChanged(string value)
        {
            text = value;
        }

        // ─── 带参数的本地化文字 ───────────────────────────────────────────────

        /// <summary>
        /// 设置格式参数并立即刷新文字，适用于 "HP: {0} / {1}" 类型的动态内容。
        /// String Table 中使用 Smart String 或 {0}{1} 占位符均可。
        /// </summary>
        public void SetArguments(params object[] args)
        {
            if (localizedKey.IsEmpty) return;
            localizedKey.Arguments = args;
            localizedKey.RefreshString();
        }
    }
}
