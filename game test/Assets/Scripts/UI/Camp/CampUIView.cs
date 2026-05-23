using IndieGame.Core.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Gameplay.Camp;
using IndieGame.Core;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 视图（View）：
    /// <para>
    /// 仅负责"如何显示"，不负责"业务编排"。具体职责包括：
    /// - 动态生成按钮并维护索引映射；
    /// - 淡入/淡出动画；
    /// - 按钮点击事件 → 转发到对应的业务事件（如 Sleep 通过 EventBus 转发为 CampSleepRequestedEvent）。
    /// </para>
    /// <para>
    /// 所有跨系统编排（恢复行动点、推进日期、自动存档、返回棋盘等）已迁移到 <see cref="CampUIController"/>。
    /// </para>
    /// </summary>
    public class CampUIView : View
    {
        [Header("Binder")]
        // UI 绑定器（负责动态菜单生成）
        [SerializeField] private CampUIBinder binder;

        [Header("Config")]
        // 默认解锁动作（可在 Inspector 中配置）
        [Tooltip("默认解锁的动作列表（可在 Inspector 配置）")]
        [SerializeField] private List<CampActionSO> defaultActions = new List<CampActionSO>();

        // CanvasGroup 控制淡入淡出
        private CanvasGroup _canvasGroup;
        // 当前解锁动作缓存（用于索引映射）
        private readonly List<CampActionSO> _currentActions = new List<CampActionSO>();

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[CampUIView] Missing binder reference.");
                return;
            }
            // 初始化 CanvasGroup
            _canvasGroup = binder.CanvasGroup != null ? binder.CanvasGroup : GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CampActionButtonClickEvent>(HandleButtonClickEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CampActionButtonClickEvent>(HandleButtonClickEvent);
        }

        /// <summary>
        /// 显示露营 UI，并淡入。
        /// </summary>
        public override void Show()
        {
            Show(defaultActions);
        }

        /// <summary>
        /// 显示并初始化菜单。
        /// </summary>
        public void Show(List<CampActionSO> unlockedActions)
        {
            // 初始化按钮列表
            InitializeMenu(unlockedActions);

            // 触发淡入动画
            StopAllCoroutines();
            StartCoroutine(FadeInRoutine());
        }

        public override void Hide()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private IEnumerator FadeInRoutine()
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.25f);
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>
        /// 动态初始化菜单：
        /// 传入已解锁动作列表，自动生成按钮。
        /// </summary>
        private void InitializeMenu(List<CampActionSO> unlockedActions)
        {
            if (binder == null || binder.MenuContainer == null || binder.ButtonPrefab == null) return;
            _currentActions.Clear();

            // 清空旧按钮
            for (int i = binder.MenuContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(binder.MenuContainer.GetChild(i).gameObject);
            }

            if (unlockedActions == null) return;

            for (int i = 0; i < unlockedActions.Count; i++)
            {
                CampActionSO action = unlockedActions[i];
                if (action == null) continue;
                _currentActions.Add(action);

                // 实例化按钮并挂到容器
                CampActionButton button = Instantiate(binder.ButtonPrefab, binder.MenuContainer);
                button.Setup(action.DisplayName, action.Icon, _currentActions.Count - 1);
            }
        }

        /// <summary>
        /// 处理按钮点击事件：
        /// 通过索引映射回动作数据，按 ActionID 转发为对应业务事件，让 Controller / 系统处理。
        /// View 自身不再调用系统级 API。
        /// </summary>
        private void HandleButtonClickEvent(CampActionButtonClickEvent evt)
        {
            if (_currentActions.Count == 0) return;
            if (evt.Index < 0 || evt.Index >= _currentActions.Count) return;
            CampActionSO action = _currentActions[evt.Index];
            if (action == null) return;
            switch (action.ActionID)
            {
                case CampActionID.Crafting:
                    // 打开打造界面：由 CraftingUIController 自行控制 show/hide。
                    EventBus.Raise(new OpenCraftingUIEvent());
                    break;
                case CampActionID.Inventory:
                    // 与 ActionMenu 走相同事件通路，由 InventoryManager 统一处理打开逻辑。
                    EventBus.Raise(new OpenInventoryEvent());
                    break;
                case CampActionID.Memory:
                    DebugTools.Log("Log: 检索语料库，查看任务记录与对话日志...");
                    break;
                case CampActionID.SkillTree:
                    DebugTools.Log("Log: 打开技能配置界面...");
                    break;
                case CampActionID.Sleep:
                    // 转发为业务事件：由 CampUIController 接管编排（黑屏 / 存档 / 返回棋盘）。
                    // View 不再直接编排跨系统流程，符合 MVB 模式职责分离。
                    DebugTools.Log("Log: 玩家请求 Sleep，事件已转发给 CampUIController。");
                    EventBus.Raise(new CampSleepRequestedEvent());
                    break;
            }
        }
    }
}
