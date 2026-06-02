using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.SkillTree;

namespace IndieGame.UI.SkillTree
{
    /// <summary>
    /// 技能树 UI 控制器（Controller）：
    /// 负责界面显隐、Tab 切换、技能节点点击、学习逻辑、EventBus 订阅。
    ///
    /// MVB 边界：
    /// - Binder：只保存 UI 引用；
    /// - View：只负责显示（节点生成 / 状态刷新 / SP 文本）；
    /// - Controller（本类）：业务编排，监听事件，调用 SkillTreeSystem。
    /// </summary>
    public class SkillTreeController : EventBusMonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SkillTreeBinder binder;
        [SerializeField] private SkillTreeView view;

        // Tab 枚举，int 值必须与 Binder 数组索引一致
        private enum SkillTreeTab { Combat = 0, Exploration = 1, Crafting = 2 }

        private SkillTreeTab _currentTab = SkillTreeTab.Exploration;
        private bool _isVisible;

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[SkillTreeController] 缺少 SkillTreeBinder 引用。");
                return;
            }
            if (view == null)
            {
                view = GetComponent<SkillTreeView>();
                if (view == null)
                    DebugTools.LogError("[SkillTreeController] 缺少 SkillTreeView 引用。");
            }

            HookTabButtons();
            HookCloseButton();
        }

        private void OnDestroy()
        {
            UnhookTabButtons();
            UnhookCloseButton();
        }

        protected override void OnEnable()
        {
            base.OnEnable(); // 调用 Bind()，注册所有 EventBus 订阅
            SetVisible(false); // 初始隐藏，等待 OpenSkillTreeUIEvent
        }

        // ─── EventBusMonoBehaviour.Bind ───────────────────────

        protected override void Bind()
        {
            Subscribe<OpenSkillTreeUIEvent>(HandleOpenSkillTreeUI);
            Subscribe<CloseSkillTreeUIEvent>(HandleCloseSkillTreeUI);
            Subscribe<SkillPointChangedEvent>(HandleSkillPointChanged);
            Subscribe<SkillLearnedEvent>(HandleSkillLearned);
        }

        // ─── 事件处理 ─────────────────────────────────────────

        private void HandleOpenSkillTreeUI(OpenSkillTreeUIEvent evt)
        {
            if (!isActiveAndEnabled) return;
            RebuildSkillGrid();
            RefreshSP();
            view?.RefreshTabHighlight((int)_currentTab);
            SetVisible(true);
        }

        private void HandleCloseSkillTreeUI(CloseSkillTreeUIEvent evt)
        {
            if (!_isVisible) return;
            SetVisible(false);
        }

        private void HandleSkillPointChanged(SkillPointChangedEvent evt)
        {
            if (!_isVisible) return;
            RefreshSP();
            // SP 变化可能导致 Locked → Available，整体刷新节点状态
            view?.RefreshAllNodeStates(GetLearnState);
        }

        private void HandleSkillLearned(SkillLearnedEvent evt)
        {
            if (!_isVisible) return;
            // 学习成功后刷新所有节点（前置关系可能解锁其他技能）
            view?.RefreshAllNodeStates(GetLearnState);
            RefreshSP();
        }

        // ─── Tab 切换 ─────────────────────────────────────────

        private void SwitchTab(SkillTreeTab tab)
        {
            if (_currentTab == tab) return;
            _currentTab = tab;
            view?.RefreshTabHighlight((int)_currentTab);
            if (_isVisible) RebuildSkillGrid();
        }

        // ─── 技能节点点击（由 View 通过回调传入 skillId）────────

        private void HandleNodeClicked(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return;

            SkillTreeSystem system = SkillTreeSystem.Instance;
            if (system == null) return;

            SkillLearnState state = system.GetLearnState(skillId);
            if (state != SkillLearnState.Available)
            {
                DebugTools.Log($"[SkillTreeController] 技能 {skillId} 当前状态 {state}，无法学习。");
                return;
            }

            bool success = system.TryLearnSkill(skillId);
            if (!success)
                DebugTools.LogWarning($"[SkillTreeController] TryLearnSkill({skillId}) 失败，请检查 SP 或前置条件。");
            // 成功时 SkillLearnedEvent 会触发 HandleSkillLearned，无需在此额外刷新。
        }

        // ─── 网格重建 ─────────────────────────────────────────

        private void RebuildSkillGrid()
        {
            SkillTreeSystem system = SkillTreeSystem.Instance;
            if (system == null || view == null) return;

            var skills = system.GetSkillsByCategory((SkillTreeCategory)(int)_currentTab);
            view.RebuildSkillGrid(skills, GetLearnState, HandleNodeClicked);
        }

        private void RefreshSP()
        {
            SkillTreeSystem system = SkillTreeSystem.Instance;
            if (system == null || view == null) return;
            view.RefreshSP(system.CurrentSP);
        }

        private SkillLearnState GetLearnState(string skillId)
        {
            SkillTreeSystem system = SkillTreeSystem.Instance;
            return system != null ? system.GetLearnState(skillId) : SkillLearnState.Locked;
        }

        // ─── 显隐 ─────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            view?.SetVisible(visible);
        }

        // ─── 按钮绑定 ─────────────────────────────────────────

        private void HookTabButtons()
        {
            if (binder?.CategoryTabButtons == null) return;
            for (int i = 0; i < binder.CategoryTabButtons.Length; i++)
            {
                if (binder.CategoryTabButtons[i] == null) continue;
                int captured = i;
                binder.CategoryTabButtons[i].onClick.AddListener(
                    () => SwitchTab((SkillTreeTab)captured));
            }
        }

        private void UnhookTabButtons()
        {
            if (binder?.CategoryTabButtons == null) return;
            foreach (var btn in binder.CategoryTabButtons)
                btn?.onClick.RemoveAllListeners();
        }

        private void HookCloseButton()
        {
            if (binder?.CloseButton != null)
                binder.CloseButton.onClick.AddListener(
                    () => EventBus.Raise(new CloseSkillTreeUIEvent()));
        }

        private void UnhookCloseButton()
        {
            binder?.CloseButton?.onClick.RemoveAllListeners();
        }
    }
}
