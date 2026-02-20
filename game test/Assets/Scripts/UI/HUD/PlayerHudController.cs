using System;
using IndieGame.Core;
using IndieGame.Gameplay.Stats;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IndieGame.UI.Hud
{
    /// <summary>
    /// 玩家 HUD 控制器（Controller / Manager）：
    /// 负责三类核心职责：
    /// 1) 监听 EventBus 事件（Health/Exp/GameMode）；
    /// 2) 执行“仅在非大世界场景显示”的规则；
    /// 3) 只把结果数据交给 View 显示，不直接操作 Binder 细节。
    ///
    /// 重要规则（按你的需求）：
    /// - HUD 仅在“非大世界场景”显示；
    /// - 大世界默认指场景名为 World；
    /// - 目前实现为：仅在 Exploration 模式且场景名不是 World 时显示；
    /// - Title / Board / Camp 模式统一隐藏。
    /// </summary>
    public class PlayerHudController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private PlayerHudView view;

        [Header("Visibility Rule")]
        [Tooltip("大世界场景名。该场景下 HUD 强制隐藏。")]
        [SerializeField] private string worldSceneName = "World";

        [Tooltip("标题场景名。标题场景下 HUD 强制隐藏。")]
        [SerializeField] private string titleSceneName = "Title";

        [Tooltip("露营场景关键字。名称中包含该关键字时 HUD 强制隐藏。")]
        [SerializeField] private string campSceneKeyword = "Camp";

        // 当前绑定的玩家对象（来自 GameManager.CurrentPlayer）。
        private GameObject _currentPlayer;
        // 当前玩家的 CharacterStats 快照引用，用于主动拉取初始显示数据。
        private CharacterStats _currentPlayerStats;

        // 当前场景规则是否允许显示 HUD。
        private bool _allowVisibleByScene;
        // 对话阶段是否强制隐藏 HUD。
        // 业务规则：只要对话开启，不管场景是否允许显示，都应先隐藏 HUD。
        private bool _forceHiddenByDialogue;
        // 是否已拿到玩家引用（用于避免每帧重复查找）。
        private bool _hasBoundPlayer;

        private void Awake()
        {
            if (view == null)
            {
                view = GetComponent<PlayerHudView>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<HealthChangedEvent>(HandleHealthChangedEvent);
            EventBus.Subscribe<ExpChangedEvent>(HandleExpChangedEvent);
            EventBus.Subscribe<GameModeChangedEvent>(HandleGameModeChangedEvent);
            EventBus.Subscribe<DialogueStartedEvent>(HandleDialogueStartedEvent);
            EventBus.Subscribe<DialogueEndedEvent>(HandleDialogueEndedEvent);
            SceneManager.sceneLoaded += HandleSceneLoaded;

            // 首次启用时执行一次可见性兜底判断：
            // 因为 HUD 可能在启动顺序上晚于 SceneLoader 的首次 GameModeChangedEvent，
            // 这时需要基于当前 ActiveScene 先给出一个安全默认状态，避免 HUD 闪现。
            EvaluateVisibilityFromActiveSceneFallback();
            ApplyVisibility();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<HealthChangedEvent>(HandleHealthChangedEvent);
            EventBus.Unsubscribe<ExpChangedEvent>(HandleExpChangedEvent);
            EventBus.Unsubscribe<GameModeChangedEvent>(HandleGameModeChangedEvent);
            EventBus.Unsubscribe<DialogueStartedEvent>(HandleDialogueStartedEvent);
            EventBus.Unsubscribe<DialogueEndedEvent>(HandleDialogueEndedEvent);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            // 玩家对象由 GameManager 在 Init 流程中运行时实例化。
            // HUD 常常会比玩家更早创建，所以这里做一次“轻量级延迟绑定”：
            // - 绑定成功后立即刷新一次 HUD；
            // - 后续不再每帧查找，避免无意义开销。
            if (_hasBoundPlayer) return;
            if (!TryBindCurrentPlayerStats()) return;

            _hasBoundPlayer = true;
            RefreshFromPlayerSnapshot();
            ApplyVisibility();
        }

        /// <summary>
        /// 处理场景模式变化：
        /// 规则为“仅 Exploration 且非 World 显示”，其他模式全部隐藏。
        /// </summary>
        private void HandleGameModeChangedEvent(GameModeChangedEvent evt)
        {
            if (evt.Mode != GameMode.Exploration)
            {
                _allowVisibleByScene = false;
            }
            else
            {
                _allowVisibleByScene = !IsWorldScene(evt.SceneName);
            }

            ApplyVisibility();

            // 切到可见场景时主动拉一次快照，避免“仅靠事件更新”造成的首帧空白。
            if (_allowVisibleByScene)
            {
                RefreshFromPlayerSnapshot();
            }
        }

        /// <summary>
        /// 场景加载兜底：
        /// 若某些时序下 GameModeChangedEvent 尚未到达，先按场景名给出保守可见性。
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EvaluateVisibilityFromSceneNameFallback(scene.name);
            ApplyVisibility();
        }

        /// <summary>
        /// 对话开始时隐藏 HUD：
        /// 需求约束是“开启对话时隐藏 HUD”，因此这里采用强制隐藏开关。
        /// </summary>
        private void HandleDialogueStartedEvent(DialogueStartedEvent evt)
        {
            _forceHiddenByDialogue = true;
            ApplyVisibility();
        }

        /// <summary>
        /// 对话结束时恢复 HUD：
        /// 恢复并不代表“必定显示”，仍需通过原有场景规则与玩家绑定规则共同判定。
        /// </summary>
        private void HandleDialogueEndedEvent(DialogueEndedEvent evt)
        {
            _forceHiddenByDialogue = false;
            ApplyVisibility();

            // 对话结束后主动刷新一次快照，避免在对话期间数值有变化但 HUD 尚未收到新事件。
            RefreshFromPlayerSnapshot();
        }

        /// <summary>
        /// 处理生命值事件：
        /// 仅接受“当前玩家”的事件，避免敌人/NPC 的生命变化污染 HUD。
        /// </summary>
        private void HandleHealthChangedEvent(HealthChangedEvent evt)
        {
            if (!IsCurrentPlayerEventOwner(evt.Owner)) return;
            if (view == null) return;
            view.RefreshHealth(evt.Current, evt.Max);
        }

        /// <summary>
        /// 处理经验值事件：
        /// 同样只接受当前玩家事件。
        /// </summary>
        private void HandleExpChangedEvent(ExpChangedEvent evt)
        {
            if (!IsCurrentPlayerEventOwner(evt.Owner)) return;
            if (view == null) return;
            view.RefreshExp(evt.Current, evt.Required);
        }

        /// <summary>
        /// 尝试绑定当前玩家的 CharacterStats。
        /// </summary>
        private bool TryBindCurrentPlayerStats()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.CurrentPlayer == null)
            {
                return false;
            }

            if (_currentPlayer == gameManager.CurrentPlayer && _currentPlayerStats != null)
            {
                return true;
            }

            _currentPlayer = gameManager.CurrentPlayer;
            _currentPlayerStats = _currentPlayer != null ? _currentPlayer.GetComponent<CharacterStats>() : null;
            return _currentPlayerStats != null;
        }

        /// <summary>
        /// 主动从玩家属性组件读取一次当前值并刷新 HUD。
        /// 用于补齐“事件尚未到来时”的首帧显示。
        /// </summary>
        private void RefreshFromPlayerSnapshot()
        {
            if (view == null) return;
            if (!TryBindCurrentPlayerStats()) return;

            view.RefreshHealth(_currentPlayerStats.CurrentHP, _currentPlayerStats.MaxHP);
            view.RefreshExp(_currentPlayerStats.CurrentEXP, _currentPlayerStats.CurrentRequiredEXP);
        }

        /// <summary>
        /// 应用最终可见性：
        /// 只有“场景允许显示”且“玩家已绑定”时才显示 HUD。
        /// </summary>
        private void ApplyVisibility()
        {
            if (view == null) return;

            bool hasPlayer = _currentPlayer != null && _currentPlayerStats != null;
            bool shouldVisible = _allowVisibleByScene && hasPlayer && !_forceHiddenByDialogue;
            view.SetVisible(shouldVisible);
        }

        /// <summary>
        /// 判断事件归属是否为当前玩家。
        /// </summary>
        private bool IsCurrentPlayerEventOwner(GameObject owner)
        {
            if (owner == null) return false;

            // 玩家可能在运行中才创建，这里每次事件都做一次轻量级兜底绑定。
            if (_currentPlayer == null || _currentPlayerStats == null)
            {
                TryBindCurrentPlayerStats();
            }

            return owner == _currentPlayer;
        }

        /// <summary>
        /// 启用时的可见性兜底：
        /// 直接读取当前活动场景名进行规则判断。
        /// </summary>
        private void EvaluateVisibilityFromActiveSceneFallback()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EvaluateVisibilityFromSceneNameFallback(activeScene.name);
        }

        /// <summary>
        /// 按场景名进行保守可见性判断：
        /// - World 隐藏；
        /// - 其他场景先按可见处理（真正规则会被后续 GameModeChangedEvent 覆盖）。
        /// </summary>
        private void EvaluateVisibilityFromSceneNameFallback(string sceneName)
        {
            if (IsWorldScene(sceneName) || IsTitleScene(sceneName) || IsCampScene(sceneName))
            {
                _allowVisibleByScene = false;
                return;
            }

            _allowVisibleByScene = true;
        }

        /// <summary>
        /// 判断是否是大世界场景（忽略大小写）。
        /// </summary>
        private bool IsWorldScene(string sceneName)
        {
            return string.Equals(sceneName, worldSceneName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断是否是标题场景（忽略大小写）。
        /// </summary>
        private bool IsTitleScene(string sceneName)
        {
            return string.Equals(sceneName, titleSceneName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断是否是露营场景：
        /// 采用关键字包含判断，兼容命名如 Camp / Camp_01 / BattleCamp 等。
        /// </summary>
        private bool IsCampScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (string.IsNullOrEmpty(campSceneKeyword)) return false;
            return sceneName.IndexOf(campSceneKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}
