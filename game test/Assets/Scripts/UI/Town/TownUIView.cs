using System.Collections;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Town;
using IndieGame.UI.Camp;

namespace IndieGame.UI.Town
{
    /// <summary>
    /// 城镇 UI 视图：
    /// 全屏覆盖的城镇菜单，包含 6 个功能按钮。
    /// 当前仅【离开】按钮有实际功能，其余按钮输出 Log 占位。
    /// </summary>
    public class TownUIView : View
    {
        [Header("Binder")]
        [SerializeField] private TownUIBinder binder;

        [Header("商店配置")]
        [SerializeField] private string _materialShopID = "town_material_shop";
        [SerializeField] private string _itemShopID     = "town_item_shop";

        [Header("旅馆 Auto Save")]
        [Tooltip("是否在住宿时自动触发一次存档。")]
        [SerializeField] private bool enableInnAutoSave = true;
        [Tooltip("住宿自动存档写入槽位。")]
        [SerializeField] private int innAutoSaveSlotIndex = 0;
        [Tooltip("住宿自动存档备注。")]
        [SerializeField] private string innAutoSaveNote = "AutoSave-Inn";
        [Tooltip("等待自动存档完成的超时时长（秒）。超时后继续流程，避免卡死。")]
        [SerializeField] private float innAutoSaveTimeoutSeconds = 8f;

        // CanvasGroup 控制淡入淡出
        private CanvasGroup _canvasGroup;

        // 旅馆自动存档请求追踪（与 CampUIView 的 Sleep 机制对称）
        private static int _innAutoSaveRequestSerial;
        private int _pendingInnAutoSaveRequestId = -1;
        private bool _pendingInnAutoSaveCompleted;
        private bool _pendingInnAutoSaveSuccess;
        private string _pendingInnAutoSaveError;

        // 硬编码按钮配置：(功能ID, 显示名称)
        private static readonly (TownActionID Id, string Label)[] ButtonDefs =
        {
            (TownActionID.MaterialShop, "素材店"),
            (TownActionID.ItemShop,     "道具店"),
            (TownActionID.Tavern,       "酒馆"),
            (TownActionID.Inn,          "旅馆"),
            (TownActionID.Teleport,     "传送"),
            (TownActionID.Leave,        "离开"),
        };

        // 当前已生成按钮对应的功能 ID 列表（用于索引映射）
        private readonly TownActionID[] _activeIds = new TownActionID[6];

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[TownUIView] 缺失 binder 引用。");
                return;
            }

            _canvasGroup = binder.CanvasGroup != null
                ? binder.CanvasGroup
                : GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 初始隐藏
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TownActionButtonClickEvent>(HandleButtonClick);
            EventBus.Subscribe<AutoSaveCompletedEvent>(HandleInnAutoSaveCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TownActionButtonClickEvent>(HandleButtonClick);
            EventBus.Unsubscribe<AutoSaveCompletedEvent>(HandleInnAutoSaveCompleted);
        }

        /// <summary>
        /// 显示城镇菜单：生成按钮并淡入。
        /// </summary>
        public override void Show()
        {
            InitializeButtons();
            StopAllCoroutines();
            StartCoroutine(FadeInRoutine());
        }

        /// <summary>
        /// 隐藏城镇菜单。
        /// </summary>
        public override void Hide()
        {
            _canvasGroup.DOKill();
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
        /// 动态生成按钮：清除旧按钮，按 ButtonDefs 顺序实例化。
        /// </summary>
        private void InitializeButtons()
        {
            if (binder == null || binder.ButtonContainer == null || binder.ButtonPrefab == null)
            {
                DebugTools.LogError("[TownUIView] binder 配置不完整，无法初始化按钮。");
                return;
            }

            // 清除旧按钮
            for (int i = binder.ButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(binder.ButtonContainer.GetChild(i).gameObject);

            // 生成新按钮
            for (int i = 0; i < ButtonDefs.Length; i++)
            {
                _activeIds[i] = ButtonDefs[i].Id;
                TownActionButton btn = Instantiate(binder.ButtonPrefab, binder.ButtonContainer);
                btn.Setup(ButtonDefs[i].Label, null, i);
            }
        }

        /// <summary>
        /// 隐藏城镇菜单并发送打开商店请求。
        /// </summary>
        private void OpenShop(string shopId)
        {
            if (string.IsNullOrWhiteSpace(shopId))
            {
                DebugTools.LogWarning("[TownUIView] 商店 ID 未配置。");
                return;
            }
            Hide();
            EventBus.Raise(new OpenShopUIRequestEvent { ShopID = shopId });
        }

        /// <summary>
        /// 旅馆住宿流程：黑屏 → 恢复行动点 → 自动存档 → 重显城镇菜单 → 淡出。
        /// 与营地 Sleep 逻辑相同，区别在于最终返回城镇菜单而非棋盘。
        /// </summary>
        private IEnumerator InnSleepRoutine()
        {
            float fadeDuration = 1f;

            // 1) 立即禁用交互，防止黑屏前重复点击
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // 2) 黑屏淡入
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
            yield return new WaitForSeconds(fadeDuration);

            // 3) 恢复全部行动点
            ActionPointSystem.Instance?.RefillActionPoints("Inn");

            // 4) 自动存档（带超时保护）
            yield return RequestInnAutoSaveRoutine();

            // 5) 在黑屏状态下重新初始化菜单按钮并恢复交互
            //    不调用 Show()，因为 Show() 内含 StopAllCoroutines() 会杀死本协程
            InitializeButtons();
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable   = true;
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.25f);
            yield return new WaitForSeconds(0.25f);

            // 6) 黑屏淡出，城镇菜单呈现
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });
        }

        /// <summary>
        /// 请求旅馆住宿自动存档并等待完成，逻辑与 CampUIView.RequestSleepAutoSaveRoutine 对称。
        /// </summary>
        private IEnumerator RequestInnAutoSaveRoutine()
        {
            if (!enableInnAutoSave) yield break;

            if (!EventBus.HasSubscribers<AutoSaveRequestedEvent>())
            {
                DebugTools.LogWarning("[TownUIView] Inn auto-save skipped: no AutoSaveRequestedEvent subscriber.");
                yield break;
            }

            _pendingInnAutoSaveCompleted = false;
            _pendingInnAutoSaveSuccess   = false;
            _pendingInnAutoSaveError     = null;
            _pendingInnAutoSaveRequestId = ++_innAutoSaveRequestSerial;

            EventBus.Raise(new AutoSaveRequestedEvent
            {
                RequestId       = _pendingInnAutoSaveRequestId,
                Reason          = AutoSaveReason.Inn,
                SlotIndex       = innAutoSaveSlotIndex,
                Note            = innAutoSaveNote,
                WaitForCompletion = true
            });

            float elapsed = 0f;
            while (!_pendingInnAutoSaveCompleted && elapsed < Mathf.Max(0.1f, innAutoSaveTimeoutSeconds))
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!_pendingInnAutoSaveCompleted)
                DebugTools.LogWarning($"[TownUIView] Inn auto-save timed out after {innAutoSaveTimeoutSeconds}s.");
            else if (!_pendingInnAutoSaveSuccess)
                DebugTools.LogWarning($"[TownUIView] Inn auto-save failed: {_pendingInnAutoSaveError}");
        }

        /// <summary>
        /// 接收自动存档完成回调，仅处理本次旅馆请求。
        /// </summary>
        private void HandleInnAutoSaveCompleted(AutoSaveCompletedEvent evt)
        {
            if (evt.RequestId != _pendingInnAutoSaveRequestId) return;
            _pendingInnAutoSaveCompleted = true;
            _pendingInnAutoSaveSuccess   = evt.Success;
            _pendingInnAutoSaveError     = evt.Error;
        }

        /// <summary>
        /// 按钮点击处理：通过索引映射到功能 ID，分派对应逻辑。
        /// </summary>
        private void HandleButtonClick(TownActionButtonClickEvent evt)
        {
            if (evt.Index < 0 || evt.Index >= ButtonDefs.Length) return;

            TownActionID id = _activeIds[evt.Index];

            switch (id)
            {
                case TownActionID.MaterialShop:
                    OpenShop(_materialShopID);
                    break;
                case TownActionID.ItemShop:
                    OpenShop(_itemShopID);
                    break;
                case TownActionID.Tavern:
                    DebugTools.Log("[城镇] 酒馆 —— 功能待实现");
                    break;
                case TownActionID.Inn:
                    StartCoroutine(InnSleepRoutine());
                    break;
                case TownActionID.Teleport:
                    DebugTools.Log("[城镇] 传送 —— 功能待实现");
                    break;
                case TownActionID.Leave:
                    // 通知 TownState 退出城镇，回到玩家回合
                    EventBus.Raise(new TownLeaveRequestedEvent());
                    break;
            }
        }
    }
}
