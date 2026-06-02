using System;
using IndieGame.Core;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Date;
using IndieGame.Gameplay.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IndieGame.UI.Hud
{
    /// <summary>
    /// 玩家 HUD 控制器：
    /// 仅在大世界场景（World）显示。
    /// 监听 HP / EXP / 行动点 / 日期事件并刷新 View。
    /// </summary>
    public class PlayerHudController : EventBusMonoBehaviour
    {
        [Header("View")]
        [SerializeField] private PlayerHudView view;
        [SerializeField] private PlayerHudBinder binder;

        [Header("场景名")]
        [SerializeField] private string worldSceneName = "World";

        // 玩家引用，用于过滤 HP / EXP 事件
        private GameObject _currentPlayer;
        private CharacterStats _currentPlayerStats;
        private bool _hasBoundPlayer;

        // 对话期间强制隐藏
        private bool _forceHiddenByDialogue;

        // 当前是否处于大世界场景
        private bool _isWorldScene;

        private void Awake()
        {
            if (view == null)
                view = GetComponent<PlayerHudView>();

            // 绑定技能树入口按钮（HUD 上的快捷打开入口）
            if (binder != null && binder.SkillTreeButton != null)
                binder.SkillTreeButton.onClick.AddListener(
                    () => EventBus.Raise(new OpenSkillTreeUIEvent()));
        }

        protected override void Bind()
        {
            Subscribe<HealthChangedEvent>(HandleHealthChanged);
            Subscribe<ExpChangedEvent>(HandleExpChanged);
            Subscribe<LevelChangedEvent>(HandleLevelChanged);
            Subscribe<ActionPointChangedEvent>(HandleActionPointChanged);
            Subscribe<DateChangedEvent>(HandleDateChanged);
            Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
            Subscribe<DialogueStartedEvent>(_ => SetDialogueHide(true));
            Subscribe<DialogueEndedEvent>(_ => { SetDialogueHide(false); RefreshAll(); });
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _isWorldScene = IsWorldScene(SceneManager.GetActiveScene().name);
            ApplyVisibility();
        }

        private void Update()
        {
            if (_hasBoundPlayer) return;
            if (!TryBindPlayer()) return;
            _hasBoundPlayer = true;
            RefreshPlayerStats();
        }

        // ─── 事件处理 ───────────────────────────────────────

        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            _isWorldScene = IsWorldScene(evt.SceneName);
            ApplyVisibility();
            if (_isWorldScene)
                RefreshAll();
        }

        private void HandleHealthChanged(HealthChangedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            if (view != null) view.RefreshHealth(evt.Current, evt.Max);
        }

        private void HandleExpChanged(ExpChangedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            if (view != null) view.RefreshExp(evt.Current, evt.Required);
        }

        private void HandleLevelChanged(LevelChangedEvent evt)
        {
            if (!IsCurrentPlayer(evt.Owner)) return;
            if (view != null) view.RefreshLevel(evt.Level);
        }

        private void HandleActionPointChanged(ActionPointChangedEvent evt)
        {
            if (view != null) view.RefreshActionPoints(evt.CurrentPoints, evt.MaxPoints);
        }

        private void HandleDateChanged(DateChangedEvent evt)
        {
            if (view != null) view.RefreshDate(evt.FormattedDate);
        }

        // ─── 可见性 ─────────────────────────────────────────

        private void SetDialogueHide(bool hide)
        {
            _forceHiddenByDialogue = hide;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (view != null)
                view.SetVisible(_isWorldScene && !_forceHiddenByDialogue);
        }

        // ─── 数据刷新 ────────────────────────────────────────

        /// <summary>
        /// 刷新所有显示数据（进入大世界时调用一次，补齐首帧空白）。
        /// </summary>
        private void RefreshAll()
        {
            RefreshPlayerStats();

            ActionPointSystem apSystem = ActionPointSystem.Instance;
            if (apSystem != null)
                view.RefreshActionPoints(apSystem.CurrentActionPoints, apSystem.MaxActionPoints);

            DateSystem dateSystem = DateSystem.Instance;
            if (dateSystem != null)
                view.RefreshDate(dateSystem.GetFormattedDate());
        }

        private void RefreshPlayerStats()
        {
            if (!TryBindPlayer()) return;
            view.RefreshHealth(_currentPlayerStats.CurrentHP, _currentPlayerStats.MaxHP);
            view.RefreshExp(_currentPlayerStats.CurrentEXP, _currentPlayerStats.CurrentRequiredEXP);
            view.RefreshLevel(_currentPlayerStats.CurrentLevel);
        }

        // ─── 工具 ────────────────────────────────────────────

        private bool TryBindPlayer()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.CurrentPlayer == null) return false;
            if (_currentPlayer == gm.CurrentPlayer && _currentPlayerStats != null) return true;

            _currentPlayer = gm.CurrentPlayer;
            _currentPlayerStats = _currentPlayer.GetComponent<CharacterStats>();
            return _currentPlayerStats != null;
        }

        private bool IsCurrentPlayer(GameObject owner)
        {
            if (owner == null) return false;
            if (_currentPlayer == null) TryBindPlayer();
            return owner == _currentPlayer;
        }

        private bool IsWorldScene(string sceneName) =>
            string.Equals(sceneName, worldSceneName, StringComparison.OrdinalIgnoreCase);
    }
}
