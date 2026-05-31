using System;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace IndieGame.UI.SystemMenu
{
    /// <summary>
    /// 系统菜单控制器：
    /// - 右下角按钮 Toggle 面板开/关；
    /// - 点击 Backdrop（面板外部）关闭面板；
    /// - 语言按钮立即通过 LocalizationSettings 切换 Locale 并刷新高亮；
    /// - 对话/Loading 期间隐藏系统按钮并强制关闭面板。
    /// </summary>
    public class SystemMenuController : EventBusMonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SystemMenuBinder binder;
        [SerializeField] private SystemMenuView   view;

        // ─── 运行时状态 ───────────────────────────────────────────────────────
        private bool _isPanelOpen;
        private bool _isHiddenByDialogue;

        // ─── 生命周期 ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[SystemMenuController] Missing SystemMenuBinder reference.", this);
                return;
            }
            if (view == null)
            {
                DebugTools.LogError("[SystemMenuController] Missing SystemMenuView reference.", this);
                return;
            }

            // 绑定按钮事件（在 OnDestroy 中移除）
            if (binder.SystemButton   != null) binder.SystemButton.onClick.AddListener(HandleSystemButtonClicked);
            if (binder.BackdropButton != null) binder.BackdropButton.onClick.AddListener(HandleBackdropClicked);
            if (binder.BtnZhHans     != null) binder.BtnZhHans.onClick.AddListener(() => HandleLanguageSelected("zh-Hans"));
            if (binder.BtnZhHant     != null) binder.BtnZhHant.onClick.AddListener(() => HandleLanguageSelected("zh-Hant"));
            if (binder.BtnEn         != null) binder.BtnEn.onClick.AddListener(()     => HandleLanguageSelected("en"));
            if (binder.BtnJa         != null) binder.BtnJa.onClick.AddListener(()     => HandleLanguageSelected("ja"));
        }

        private void OnDestroy()
        {
            if (binder == null) return;
            if (binder.SystemButton   != null) binder.SystemButton.onClick.RemoveAllListeners();
            if (binder.BackdropButton != null) binder.BackdropButton.onClick.RemoveAllListeners();
            if (binder.BtnZhHans     != null) binder.BtnZhHans.onClick.RemoveAllListeners();
            if (binder.BtnZhHant     != null) binder.BtnZhHant.onClick.RemoveAllListeners();
            if (binder.BtnEn         != null) binder.BtnEn.onClick.RemoveAllListeners();
            if (binder.BtnJa         != null) binder.BtnJa.onClick.RemoveAllListeners();
        }

        protected override void OnEnable()
        {
            base.OnEnable(); // 调用 Bind() 注册 EventBus 事件
            RefreshHighlight();
        }

        // ─── EventBus 绑定 ────────────────────────────────────────────────────

        protected override void Bind()
        {
            Subscribe<DialogueStartedEvent>(_ => OnDialogueStateChanged(true));
            Subscribe<DialogueEndedEvent>(_ => OnDialogueStateChanged(false));
            Subscribe<GameStateChangedEvent>(HandleGameStateChanged);
        }

        // ─── 按钮回调 ─────────────────────────────────────────────────────────

        private void HandleSystemButtonClicked()
        {
            if (_isPanelOpen) ClosePanel();
            else              OpenPanel();
        }

        private void HandleBackdropClicked()
        {
            ClosePanel();
        }

        // ─── 面板开关 ─────────────────────────────────────────────────────────

        private void OpenPanel()
        {
            if (_isPanelOpen || view == null) return;
            _isPanelOpen = true;
            view.ShowPanel();
            RefreshHighlight();
            EventBus.Raise(new SystemMenuOpenedEvent());
        }

        private void ClosePanel()
        {
            if (!_isPanelOpen || view == null) return;
            _isPanelOpen = false;
            view.HidePanel();
            EventBus.Raise(new SystemMenuClosedEvent());
        }

        // ─── 语言切换 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 立即切换 Locale 并刷新高亮。
        /// LocalizationSettings.SelectedLocale 赋值后，Unity Localization 包异步刷新 StringTable；
        /// FontLocalizationSetter 已订阅 SelectedLocaleChanged 事件，字体会自动切换。
        /// </summary>
        private void HandleLanguageSelected(string localeCode)
        {
            Locale target = FindLocale(localeCode);
            if (target == null)
            {
                DebugTools.LogWarning($"[SystemMenuController] Locale not found: {localeCode}", this);
                return;
            }

            LocalizationSettings.SelectedLocale = target;
            // 立即刷新高亮，面板保持打开方便玩家继续切换
            view.RefreshLanguageHighlight(localeCode);
        }

        /// <summary>
        /// 从 AvailableLocales 中精确匹配 localeCode（IETF 标签，如 "zh-Hans"）。
        /// 若 Localization 尚未初始化完毕则返回 null。
        /// </summary>
        private static Locale FindLocale(string localeCode)
        {
            var locales = LocalizationSettings.AvailableLocales?.Locales;
            if (locales == null) return null;

            foreach (var locale in locales)
            {
                if (locale == null) continue;
                if (string.Equals(locale.Identifier.Code, localeCode,
                                  StringComparison.OrdinalIgnoreCase))
                    return locale;
            }
            return null;
        }

        // ─── 可见性控制 ───────────────────────────────────────────────────────

        private void OnDialogueStateChanged(bool dialogueActive)
        {
            _isHiddenByDialogue = dialogueActive;
            // 对话开始时顺手关闭面板，避免面板悬浮在对话 UI 下方
            if (dialogueActive && _isPanelOpen) ClosePanel();
            ApplyButtonVisibility();
        }

        private void HandleGameStateChanged(GameStateChangedEvent evt)
        {
            bool shouldHide = evt.NewState == GameState.Loading
                           || evt.NewState == GameState.Initialization;
            if (shouldHide && _isPanelOpen) ClosePanel();
            if (view != null)
                view.SetButtonVisible(!shouldHide && !_isHiddenByDialogue);
        }

        private void ApplyButtonVisibility()
        {
            if (view != null)
                view.SetButtonVisible(!_isHiddenByDialogue);
        }

        // ─── 高亮工具 ─────────────────────────────────────────────────────────

        private void RefreshHighlight()
        {
            if (view == null) return;
            Locale current = LocalizationSettings.SelectedLocale;
            if (current == null) return;
            view.RefreshLanguageHighlight(current.Identifier.Code);
        }
    }
}
