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

        [Header("Sleep Auto Save")]
        [Tooltip("是否在执行 Sleep 时自动触发一次存档。")]
        [SerializeField] private bool enableSleepAutoSave = true;

        [Tooltip("Sleep 自动存档写入槽位。")]
        [SerializeField] private int sleepAutoSaveSlotIndex = 0;

        [Tooltip("Sleep 自动存档备注（用于标题读档列表识别该存档来源）。")]
        [SerializeField] private string sleepAutoSaveNote = "AutoSave-Sleep";

        [Tooltip("等待自动存档完成的超时时长（秒）。超时后会继续返回棋盘，避免流程卡死。")]
        [SerializeField] private float sleepAutoSaveTimeoutSeconds = 8f;

        // CanvasGroup 控制淡入淡出
        private CanvasGroup _canvasGroup;
        // 当前解锁动作缓存（用于索引映射）
        private readonly List<CampActionSO> _currentActions = new List<CampActionSO>();

        // 自动存档请求递增序号（静态）：用于把“本次 Sleep 请求”与“完成事件”精准匹配。
        private static int _sleepAutoSaveRequestSerial;
        // 当前 Sleep 流程正在等待的 RequestId（-1 表示当前没有等待中的自动存档）。
        private int _pendingSleepAutoSaveRequestId = -1;
        // 当前等待请求是否已收到 AutoSaveCompletedEvent 回调。
        private bool _pendingSleepAutoSaveCompleted;
        // 当前等待请求成功标记。
        private bool _pendingSleepAutoSaveSuccess;
        // 当前等待请求错误信息。
        private string _pendingSleepAutoSaveError;

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
            EventBus.Subscribe<AutoSaveCompletedEvent>(HandleSleepAutoSaveCompletedEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CampActionButtonClickEvent>(HandleButtonClickEvent);
            EventBus.Unsubscribe<AutoSaveCompletedEvent>(HandleSleepAutoSaveCompletedEvent);
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
                    // 打开打造界面：
                    // 按架构要求通过 EventBus 通知，由 CraftingUIController 自行控制 show/hide。
                    EventBus.Raise(new OpenCraftingUIEvent());
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

            // 3) 在返回棋盘前执行一次自动存档：
            //    注意这里不直接调用 SaveManager，而是通过 EventBus 请求全局 AutoSaveService 处理。
            //    这样可保证 CampUIView 只负责编排流程，不承担存档策略与执行细节。
            yield return RequestSleepAutoSaveRoutine();

            // 4) 关闭露营 UI（避免与棋盘 UI 叠加）
            Hide();

            // 5) 返回棋盘（不重复触发淡入淡出）
            if (SceneLoader.Instance != null)
            {
                yield return SceneLoader.Instance.ReturnToBoardRoutine(false, fadeDuration, false);
            }

            // 6) 黑屏淡出（等待棋盘加载完成后执行）
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndLoading();
            }
        }

        /// <summary>
        /// 请求并等待“睡觉自动存档”完成：
        /// - 若关闭自动存档：直接跳过；
        /// - 若无监听方：记录警告并跳过；
        /// - 若超时：记录警告并继续流程，避免卡死在黑屏。
        /// </summary>
        private IEnumerator RequestSleepAutoSaveRoutine()
        {
            if (!enableSleepAutoSave) yield break;

            if (!EventBus.HasSubscribers<AutoSaveRequestedEvent>())
            {
                Debug.LogWarning("[CampUIView] Sleep auto-save skipped: no AutoSaveRequestedEvent subscriber.");
                yield break;
            }

            _pendingSleepAutoSaveCompleted = false;
            _pendingSleepAutoSaveSuccess = false;
            _pendingSleepAutoSaveError = null;
            _pendingSleepAutoSaveRequestId = ++_sleepAutoSaveRequestSerial;

            EventBus.Raise(new AutoSaveRequestedEvent
            {
                RequestId = _pendingSleepAutoSaveRequestId,
                Reason = AutoSaveReason.Sleep,
                SlotIndex = sleepAutoSaveSlotIndex,
                Note = sleepAutoSaveNote,
                // Sleep 流程需要在黑屏阶段等待自动存档结果，再继续返回棋盘。
                WaitForCompletion = true
            });

            float elapsed = 0f;
            while (!_pendingSleepAutoSaveCompleted && elapsed < Mathf.Max(0.1f, sleepAutoSaveTimeoutSeconds))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_pendingSleepAutoSaveCompleted)
            {
                Debug.LogWarning("[CampUIView] Sleep auto-save timed out. Continue return-to-board flow.");
            }
            else if (!_pendingSleepAutoSaveSuccess)
            {
                Debug.LogWarning($"[CampUIView] Sleep auto-save failed: {_pendingSleepAutoSaveError}");
            }

            _pendingSleepAutoSaveRequestId = -1;
        }

        /// <summary>
        /// 接收自动存档完成事件：
        /// 仅处理“当前 Sleep 流程等待中的 requestId”，避免其他系统的存档结果污染当前流程。
        /// </summary>
        private void HandleSleepAutoSaveCompletedEvent(AutoSaveCompletedEvent evt)
        {
            if (_pendingSleepAutoSaveRequestId < 0) return;
            if (evt.RequestId != _pendingSleepAutoSaveRequestId) return;
            if (evt.Reason != AutoSaveReason.Sleep) return;

            _pendingSleepAutoSaveCompleted = true;
            _pendingSleepAutoSaveSuccess = evt.Success;
            _pendingSleepAutoSaveError = evt.Error;
        }
    }
}
