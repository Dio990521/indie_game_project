using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.Data;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Town;
using IndieGame.UI.Camp;
using IndieGame.UI.Treasure;

namespace IndieGame.UI.Town
{
    /// <summary>
    /// 城镇 UI 视图：全屏覆盖的城镇菜单，包含 6 个功能按钮。
    /// 支持背景图（由 TownTile.townBackground 配置）、传送选单（复用 TreasureMenuView.ShowSimple）。
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

        // 当前城镇数据（由 TownState.OnEnter → Configure() 注入）
        private int _currentNodeId = -1;
        private TownTile _currentTownTile;

        // 传送流程：字段级委托，OnDisable 兜底清理（协程被 StopAllCoroutines 终止时不执行 finally）
        private System.Action<TreasureItemSelectedEvent>  _onTeleportSelected;
        private System.Action<TreasureMenuCancelledEvent> _onTeleportCancelled;

        // 传送协程的轮询标志
        private bool _teleportNodeSelected;
        private bool _teleportCancelled;
        private int  _teleportTargetNodeId;

        // 旅馆自动存档请求追踪（与 CampUIView 的 Sleep 机制对称）
        private static int _innAutoSaveRequestSerial;
        private int _pendingInnAutoSaveRequestId = -1;
        private bool _pendingInnAutoSaveCompleted;
        private bool _pendingInnAutoSaveSuccess;
        private string _pendingInnAutoSaveError;

        // 长协程互斥引用（非 null 即代表对应流程正在进行）。
        // 旅馆住宿与传送是两条独立流程，玩家在黑屏/淡入期间快速重复点击同一按钮会触发
        // 并行协程：旅馆并行 → 日期推进两次；传送并行 → 两个 TreasureMenu 互相覆盖。
        private Coroutine _innSleepCoroutine;
        private Coroutine _teleportCoroutine;

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

            // 兜底清理传送委托（协程被外部 StopAllCoroutines 终止时不会执行正常的注销路径）
            if (_onTeleportSelected != null)
            {
                EventBus.Unsubscribe(_onTeleportSelected);
                _onTeleportSelected = null;
            }
            if (_onTeleportCancelled != null)
            {
                EventBus.Unsubscribe(_onTeleportCancelled);
                _onTeleportCancelled = null;
            }

            // 兜底：组件被禁用/销毁时强制停止旅馆与传送协程，并清空互斥引用，
            // 否则下次启用 View 时陈旧的 Coroutine 引用会让互斥保护误判为"已在执行"。
            if (_innSleepCoroutine != null)
            {
                StopCoroutine(_innSleepCoroutine);
                _innSleepCoroutine = null;
            }
            if (_teleportCoroutine != null)
            {
                StopCoroutine(_teleportCoroutine);
                _teleportCoroutine = null;
            }

            // 自动存档等待状态一并复位，避免下次回到城镇时旧 RequestId 错误匹配。
            _pendingInnAutoSaveRequestId = -1;
            _pendingInnAutoSaveCompleted = false;
            _pendingInnAutoSaveSuccess = false;
            _pendingInnAutoSaveError = null;
        }

        /// <summary>
        /// 配置当前城镇数据，在 Show() 之前由 TownState.OnEnter 调用。
        /// 传送完成后也会调用此方法热切换背景图，无需退出重进 TownState。
        /// </summary>
        public void Configure(int nodeId, TownTile townTile)
        {
            _currentNodeId   = nodeId;
            _currentTownTile = townTile;

            if (binder != null && binder.BgImage != null)
            {
                Sprite bg = townTile != null ? townTile.townBackground : null;
                binder.BgImage.sprite  = bg;
                binder.BgImage.enabled = bg != null;
            }
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

            // 3) 恢复全部行动点，推进游戏日期
            ActionPointSystem.Instance?.RefillActionPoints("Inn");
            IndieGame.Gameplay.Date.DateSystem.Instance?.AdvanceDay();

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

            // 正常完成：清空互斥引用，允许下一次旅馆住宿。
            // 协程被 OnDisable 中途 Stop 的情况由 OnDisable 兜底清理。
            _innSleepCoroutine = null;
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
                    // 互斥保护：旅馆 Sleep 包含黑屏淡入、自动存档等待、推进日期等多步骤，
                    // 玩家若在中途重复点击会触发并行流程（日期推进两次 / 存档冲突）。
                    if (_innSleepCoroutine != null)
                    {
                        DebugTools.LogWarning("[TownUIView] 旅馆住宿流程已在进行中，忽略重复请求。");
                        break;
                    }
                    _innSleepCoroutine = StartCoroutine(InnSleepRoutine());
                    break;
                case TownActionID.Teleport:
                    // 互斥保护：传送菜单的事件委托是字段级单实例，若并发启动两个传送协程，
                    // 第二个协程会覆盖第一个的委托引用，导致前者残留订阅无法清理。
                    if (_teleportCoroutine != null)
                    {
                        DebugTools.LogWarning("[TownUIView] 传送流程已在进行中，忽略重复请求。");
                        break;
                    }
                    _teleportCoroutine = StartCoroutine(TeleportMenuRoutine());
                    break;
                case TownActionID.Leave:
                    // 通知 TownState 退出城镇，回到玩家回合
                    EventBus.Raise(new TownLeaveRequestedEvent());
                    break;
            }
        }
        /// <summary>
        /// 城镇传送流程：
        /// 1. 收集已解锁城镇（排除当前）→ 2. 弹出选单（复用 TreasureMenuView.ShowSimple）
        /// → 3. 等待选择 → 4. 黑屏淡入 → 5. 移动玩家 + 更新背景图 → 6. 黑屏淡出，显示目标城镇菜单。
        /// 全程不退出 TownState，传送后直接热更新城镇数据。
        /// </summary>
        private IEnumerator TeleportMenuRoutine()
        {
            // 用 try/finally 包裹整个协程，确保无论从哪一个 yield break 退出，
            // 都能复位互斥引用并彻底清理事件委托。这样：
            // - 取消传送、目标列表为空、UI 资源缺失等异常分支不会留下"已在执行"的伪状态；
            // - 也避免委托订阅在异常路径上残留。
            // 注：try/finally 中可以包含 yield return，但 try/catch 不可，因此这里只用 finally。
            try
            {
            // ① 获取已解锁城镇列表（排除当前城镇）
            var unlockMgr = TownUnlockManager.Instance;
            if (unlockMgr == null)
            {
                DebugTools.LogWarning("[城镇] TownUnlockManager 未就绪，传送中止。");
                yield break;
            }

            List<(int nodeId, TownTile tile)> targets = unlockMgr.GetUnlockedTowns(excludeNodeId: _currentNodeId);
            if (targets.Count == 0)
            {
                DebugTools.Log("[城镇] 没有其他已解锁城镇，无法传送。");
                yield break;
            }

            var menu = UIManager.Instance != null ? UIManager.Instance.TreasureMenuInstance : null;
            if (menu == null)
            {
                DebugTools.LogWarning("[城镇] TreasureMenuInstance 未就绪，传送中止。");
                yield break;
            }

            // ② 淡出城镇菜单（防止遮挡传送选单），并禁用交互
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.DOKill();
            _canvasGroup.DOFade(0f, 0.15f);
            // 同时将传送选单移到 UI 层最顶层，确保渲染在最前
            UIManager.Instance.TreasureMenuInstance.transform.SetAsLastSibling();
            yield return new WaitForSeconds(0.15f);  // 等待淡出完成再显示选单

            // ③ 注册字段级委托（OnDisable 时兜底清理）
            _teleportNodeSelected  = false;
            _teleportCancelled     = false;
            _teleportTargetNodeId  = -1;

            _onTeleportSelected = evt =>
            {
                if (int.TryParse(evt.TreasureId, out int id))
                {
                    _teleportTargetNodeId = id;
                    _teleportNodeSelected = true;
                }
            };
            _onTeleportCancelled = _ => { _teleportCancelled = true; };
            EventBus.Subscribe(_onTeleportSelected);
            EventBus.Subscribe(_onTeleportCancelled);

            // ④ 构建条目并展示（复用 TreasureMenuView.ShowSimple，与斗篷宝具传送同一模式）
            var items = new List<SimpleMenuItem>(targets.Count);
            foreach (var (nodeId, tile) in targets)
                items.Add(new SimpleMenuItem { Id = nodeId.ToString(), DisplayText = tile != null ? tile.townName : $"城镇 #{nodeId}" });
            menu.ShowSimple(items);

            // ⑤ 等待玩家选择或取消
            while (!_teleportNodeSelected && !_teleportCancelled)
                yield return null;

            // ⑥ 正常路径注销委托（OnDisable 是兜底）
            EventBus.Unsubscribe(_onTeleportSelected);  _onTeleportSelected  = null;
            EventBus.Unsubscribe(_onTeleportCancelled); _onTeleportCancelled = null;

            if (_teleportCancelled)
            {
                // 取消：将城镇菜单淡回来并恢复交互
                _canvasGroup.DOKill();
                _canvasGroup.DOFade(1f, 0.15f);
                yield return new WaitForSeconds(0.15f);
                _canvasGroup.interactable   = true;
                _canvasGroup.blocksRaycasts = true;
                yield break;
            }

            // ⑦ 找到目标 TownTile
            TownTile targetTile = null;
            foreach (var (nodeId, tile) in targets)
            {
                if (nodeId == _teleportTargetNodeId) { targetTile = tile; break; }
            }

            // ⑧ 黑屏淡入
            float fadeDuration = 1f;
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
            yield return new WaitForSeconds(fadeDuration);

            // ⑨ 移动玩家到目标节点
            var board = BoardGameManager.Instance;
            if (board != null)
                board.movementController?.SetCurrentNodeById(_teleportTargetNodeId);

            // ⑩ 同步相机
            if (CameraManager.Instance != null && GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
            {
                CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
                CameraManager.Instance.WarpCameraToTarget();
            }

            // ⑪ 在黑屏期间热更新城镇数据（背景图切换）
            Configure(_teleportTargetNodeId, targetTile);

            // ⑫ 在黑屏内重新初始化按钮并恢复交互
            //    不调用 Show()，因为 Show() 内含 StopAllCoroutines() 会杀死本协程
            InitializeButtons();
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable   = true;
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.25f);
            yield return new WaitForSeconds(0.25f);

            // ⑬ 黑屏淡出，目标城镇菜单呈现（背景图已在 Configure 中切换）
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });

            DebugTools.Log($"[城镇] 传送完成 → {targetTile?.townName ?? $"节点 {_teleportTargetNodeId}"}");
            }
            finally
            {
                // 任何退出路径（正常完成 / 取消 / 异常 / unlockMgr 缺失等）都执行：
                // 1) 清空互斥引用，允许下一次传送；
                // 2) 兜底清理字段级事件委托，防止在异常分支上残留订阅。
                _teleportCoroutine = null;

                if (_onTeleportSelected != null)
                {
                    EventBus.Unsubscribe(_onTeleportSelected);
                    _onTeleportSelected = null;
                }
                if (_onTeleportCancelled != null)
                {
                    EventBus.Unsubscribe(_onTeleportCancelled);
                    _onTeleportCancelled = null;
                }
            }
        }
    }
}
