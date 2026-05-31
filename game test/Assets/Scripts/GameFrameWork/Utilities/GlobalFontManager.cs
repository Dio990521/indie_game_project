using System.Collections.Generic;
using IndieGame.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 全局字体管理器：
    /// 监听语言切换事件，统一将字体推送给所有已注册的 LocalizedText。
    /// 只需在场景中挂一个（跨场景常驻），无需给每个文字组件单独配置字体。
    /// </summary>
    public class GlobalFontManager : MonoSingleton<GlobalFontManager>
    {
        [Header("每种语言对应的字体资产")]
        [SerializeField] private TMP_FontAsset fontZhHans;
        [SerializeField] private TMP_FontAsset fontZhHant;
        [SerializeField] private TMP_FontAsset fontEn;
        [SerializeField] private TMP_FontAsset fontJa;

        // 所有激活中的 LocalizedText 注册表
        private readonly List<LocalizedText> _registry = new();

        /// <summary>当前生效的字体，LocalizedText 在 Awake 时主动拉取。</summary>
        public TMP_FontAsset CurrentFont { get; private set; }

        protected override bool KeepAcrossScenes => true;

        // ─── 生命周期 ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void Start()
        {
            // Start 时所有场景对象的 Awake 已执行完毕，注册表已满，统一推送一次
            ApplyFont(LocalizationSettings.SelectedLocale);
        }

        // ─── 注册表 ───────────────────────────────────────────────────────────

        /// <summary>
        /// LocalizedText 在 Awake 时调用，完成注册并立即同步当前字体。
        /// 若 CurrentFont 尚未初始化（首帧 Awake 阶段），等 Start 统一推送。
        /// </summary>
        public void Register(LocalizedText text)
        {
            if (text == null) return;
            _registry.Add(text);
            // 动态实例化场景：CurrentFont 已有值，立即应用
            if (CurrentFont != null)
                text.ApplyFont(CurrentFont);
        }

        public void Unregister(LocalizedText text)
        {
            _registry.Remove(text);
        }

        // ─── 字体切换 ─────────────────────────────────────────────────────────

        private void OnLocaleChanged(Locale locale) => ApplyFont(locale);

        private void ApplyFont(Locale locale)
        {
            CurrentFont = ResolveFont(locale?.Identifier.Code);
            if (CurrentFont == null) return;

            // 同步修改 TMP 全局默认值，使之后 Instantiate 的 TMP_Text 也用新字体
            TMP_Settings.defaultFontAsset = CurrentFont;

            foreach (var text in _registry)
            {
                if (text != null) text.ApplyFont(CurrentFont);
            }
        }

        private TMP_FontAsset ResolveFont(string localeCode) => localeCode switch
        {
            "zh-Hans" => fontZhHans,
            "zh-Hant" => fontZhHant,
            "ja"      => fontJa,
            _         => fontEn
        };
    }
}
