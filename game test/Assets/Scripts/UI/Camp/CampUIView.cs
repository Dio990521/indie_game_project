using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Gameplay.Camp;
using IndieGame.Core;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 视图：
    /// 负责显示/隐藏露营菜单，并在 Show 时做淡入动画。
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
                Debug.LogError("[CampUIView] Missing binder reference.");
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
        /// 通过索引映射回动作数据，实现解耦。
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
                    Debug.Log("Log: 消耗大量时间，开启制作...");
                    break;
                case CampActionID.Inventory:
                    Debug.Log("Log: 打开已有 Inventory 界面...");
                    break;
                case CampActionID.Memory:
                    Debug.Log("Log: 检索语料库，查看任务记录与对话日志...");
                    break;
                case CampActionID.SkillTree:
                    Debug.Log("Log: 打开技能配置界面...");
                    break;
                case CampActionID.ShopManagement:
                    Debug.Log("Log: 极少量消耗时间，飞鸽传书远程管理...");
                    break;
                case CampActionID.Sleep:
                    Debug.Log("Log: 根据剩余时间计算翌日状态，执行黑屏转场退出露营...");
                    StartCoroutine(SleepRoutine());
                    break;
            }
        }

        private IEnumerator SleepRoutine()
        {
            float fadeDuration = 1f;
            // 1) 黑屏淡入
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
            yield return new WaitForSeconds(fadeDuration);

            // 2) 执行数值结算（占位）
            // TODO: 在此加入睡觉结算逻辑（时间推进/状态恢复等）

            // 3) 关闭露营 UI（避免与棋盘 UI 叠加）
            Hide();

            // 4) 返回棋盘（不重复触发淡入淡出）
            if (SceneLoader.Instance != null)
            {
                yield return SceneLoader.Instance.ReturnToBoardRoutine(false, fadeDuration);
            }

            // 5) 黑屏淡出（等待棋盘加载完成后执行）
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });
        }
    }
}
