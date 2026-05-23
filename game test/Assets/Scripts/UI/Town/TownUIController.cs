using System;
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
using IndieGame.UI.Treasure;

namespace IndieGame.UI.Town
{
    /// <summary>
    /// 城镇 UI 控制器（Controller）：
    /// <para>
    /// 负责城镇相关的两条业务流程：
    /// 1) 旅馆住宿（Inn）：黑屏 → 恢复行动点 → 推进日期 → 自动存档 → 黑屏淡出（停留城镇）；
    /// 2) 城镇传送（Teleport）：显示选单 → 等待选择 → 黑屏 → 移动玩家 → 同步相机 → 切换背景 → 黑屏淡出。
    /// </para>
    /// <para>
    /// MVB 边界说明：
    /// - View 仅负责显示菜单 / 淡入淡出 / 接收按钮点击 / 暴露当前城镇上下文（nodeId、TownTile、CanvasGroup）；
    /// - Controller（本类）负责所有跨系统编排（ActionPointSystem / DateSystem / SceneLoader /
    ///   BoardMovementController / CameraManager / TownUnlockManager / AutoSaveService）。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(TownUIView))]
    public class TownUIController : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("受控的 TownUIView 引用。Awake 时若为空会自动从同 GameObject 上获取。")]
        [SerializeField] private TownUIView view;

        [Header("旅馆 Auto Save")]
        [Tooltip("是否在住宿时自动触发一次存档。")]
        [SerializeField] private bool enableInnAutoSave = true;
        [Tooltip("住宿自动存档写入槽位。")]
        [SerializeField] private int innAutoSaveSlotIndex = 0;
        [Tooltip("住宿自动存档备注。")]
        [SerializeField] private string innAutoSaveNote = "AutoSave-Inn";
        [Tooltip("等待自动存档完成的超时时长（秒）。超时后继续流程，避免卡死。")]
        [SerializeField] private float innAutoSaveTimeoutSeconds = 8f;

        // 旅馆自动存档请求追踪（与 CampUIController 的 Sleep 机制对称）。
        private static int _innAutoSaveRequestSerial;
        private int _pendingInnAutoSaveRequestId = -1;
        private bool _pendingInnAutoSaveCompleted;
        private bool _pendingInnAutoSaveSuccess;
        private string _pendingInnAutoSaveError;

        // 长协程互斥引用：旅馆住宿与传送是两条独立流程，分别拥有互斥引用。
        private Coroutine _innSleepCoroutine;
        private Coroutine _teleportCoroutine;

        // 传送菜单的字段级事件委托：协程内动态订阅 TreasureMenu 选择/取消事件。
        // 字段化是为了让 OnDisable 兜底反订阅，避免协程被外部 Stop 时残留订阅。
        private Action<TreasureItemSelectedEvent> _onTeleportSelected;
        private Action<TreasureMenuCancelledEvent> _onTeleportCancelled;
        // 传送等待标志（由委托设置，协程轮询）。
        private bool _teleportNodeSelected;
        private bool _teleportCancelled;
        private int _teleportTargetNodeId;

        private void Awake()
        {
            if (view == null)
            {
                // RequireComponent 保证同 GameObject 上必有 TownUIView。
                view = GetComponent<TownUIView>();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InnSleepRequestedEvent>(HandleInnSleepRequested);
            EventBus.Subscribe<TownTeleportRequestedEvent>(HandleTeleportRequested);
            EventBus.Subscribe<AutoSaveCompletedEvent>(HandleInnAutoSaveCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InnSleepRequestedEvent>(HandleInnSleepRequested);
            EventBus.Unsubscribe<TownTeleportRequestedEvent>(HandleTeleportRequested);
            EventBus.Unsubscribe<AutoSaveCompletedEvent>(HandleInnAutoSaveCompleted);

            // 兜底清理传送委托（协程被 StopCoroutine 终止时不会执行 finally）。
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

            // 兜底：组件被禁用/销毁时强制停止旅馆与传送协程，并复位互斥引用。
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

        // ===================== 旅馆住宿 =====================

        /// <summary>
        /// 处理旅馆住宿业务请求。互斥保护 + 启动协程。
        /// </summary>
        private void HandleInnSleepRequested(InnSleepRequestedEvent evt)
        {
            if (_innSleepCoroutine != null)
            {
                DebugTools.LogWarning("[TownUIController] 旅馆住宿流程已在进行中，忽略重复请求。");
                return;
            }
            _innSleepCoroutine = StartCoroutine(InnSleepRoutine());
        }

        /// <summary>
        /// 旅馆住宿协程：黑屏 → 恢复行动点 → 推进日期 → 自动存档 → 重显菜单 → 黑屏淡出。
        /// </summary>
        private IEnumerator InnSleepRoutine()
        {
            CanvasGroup canvas = view != null ? view.CanvasGroup : null;
            float fadeDuration = 1f;

            // 1) 立即禁用交互，防止黑屏前重复点击。
            if (canvas != null)
            {
                canvas.interactable = false;
                canvas.blocksRaycasts = false;
            }

            // 2) 黑屏淡入。
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
            yield return new WaitForSeconds(fadeDuration);

            // 3) 恢复全部行动点，推进游戏日期。
            ActionPointSystem.Instance?.RefillActionPoints("Inn");
            IndieGame.Gameplay.Date.DateSystem.Instance?.AdvanceDay();

            // 4) 自动存档（带超时保护）。
            yield return RequestInnAutoSaveRoutine();

            // 5) 在黑屏状态下重新初始化菜单按钮并恢复交互。
            //    通过 View 的 public 方法触发，View 自身依然保持纯显示职责。
            if (view != null)
            {
                view.RebuildMenuForOverlay();
            }
            if (canvas != null)
            {
                canvas.blocksRaycasts = true;
                canvas.interactable = true;
                canvas.DOKill();
                canvas.alpha = 0f;
                canvas.DOFade(1f, 0.25f);
            }
            yield return new WaitForSeconds(0.25f);

            // 6) 黑屏淡出，城镇菜单呈现。
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });

            // 流程正常结束：清空互斥标记。
            _innSleepCoroutine = null;
        }

        /// <summary>
        /// 请求旅馆住宿自动存档并等待完成。
        /// </summary>
        private IEnumerator RequestInnAutoSaveRoutine()
        {
            if (!enableInnAutoSave) yield break;

            if (!EventBus.HasSubscribers<AutoSaveRequestedEvent>())
            {
                DebugTools.LogWarning("[TownUIController] Inn auto-save skipped: no AutoSaveRequestedEvent subscriber.");
                yield break;
            }

            _pendingInnAutoSaveCompleted = false;
            _pendingInnAutoSaveSuccess = false;
            _pendingInnAutoSaveError = null;
            _pendingInnAutoSaveRequestId = ++_innAutoSaveRequestSerial;

            EventBus.Raise(new AutoSaveRequestedEvent
            {
                RequestId = _pendingInnAutoSaveRequestId,
                Reason = AutoSaveReason.Inn,
                SlotIndex = innAutoSaveSlotIndex,
                Note = innAutoSaveNote,
                WaitForCompletion = true
            });

            float elapsed = 0f;
            while (!_pendingInnAutoSaveCompleted && elapsed < Mathf.Max(0.1f, innAutoSaveTimeoutSeconds))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_pendingInnAutoSaveCompleted)
            {
                DebugTools.LogWarning($"[TownUIController] Inn auto-save timed out after {innAutoSaveTimeoutSeconds}s.");
            }
            else if (!_pendingInnAutoSaveSuccess)
            {
                DebugTools.LogWarning($"[TownUIController] Inn auto-save failed: {_pendingInnAutoSaveError}");
            }

            _pendingInnAutoSaveRequestId = -1;
        }

        /// <summary>
        /// 接收自动存档完成回调，仅处理本次旅馆请求（用 RequestId 精确匹配，避免被其他来源串线）。
        /// </summary>
        private void HandleInnAutoSaveCompleted(AutoSaveCompletedEvent evt)
        {
            if (_pendingInnAutoSaveRequestId < 0) return;
            if (evt.RequestId != _pendingInnAutoSaveRequestId) return;
            if (evt.Reason != AutoSaveReason.Inn) return;

            _pendingInnAutoSaveCompleted = true;
            _pendingInnAutoSaveSuccess = evt.Success;
            _pendingInnAutoSaveError = evt.Error;
        }

        // ===================== 城镇传送 =====================

        /// <summary>
        /// 处理城镇传送业务请求。互斥保护 + 启动协程。
        /// </summary>
        private void HandleTeleportRequested(TownTeleportRequestedEvent evt)
        {
            if (_teleportCoroutine != null)
            {
                DebugTools.LogWarning("[TownUIController] 传送流程已在进行中，忽略重复请求。");
                return;
            }
            _teleportCoroutine = StartCoroutine(TeleportMenuRoutine());
        }

        /// <summary>
        /// 城镇传送协程：
        /// 1. 收集已解锁城镇（排除当前）→ 2. 弹出选单 → 3. 等待选择 → 4. 黑屏淡入
        /// → 5. 移动玩家 + 切换背景 → 6. 黑屏淡出，显示目标城镇菜单。
        /// 全程通过 try/finally 保护，确保任何退出路径都能复位互斥引用并清理事件委托。
        /// </summary>
        private IEnumerator TeleportMenuRoutine()
        {
            CanvasGroup canvas = view != null ? view.CanvasGroup : null;
            int currentNodeId = view != null ? view.CurrentNodeId : -1;

            // 注：try/finally 中可以包含 yield return，但 try/catch 不可，因此这里只用 finally。
            try
            {
                // ① 获取已解锁城镇列表（排除当前城镇）。
                var unlockMgr = TownUnlockManager.Instance;
                if (unlockMgr == null)
                {
                    DebugTools.LogWarning("[TownUIController] TownUnlockManager 未就绪，传送中止。");
                    yield break;
                }

                List<(int nodeId, TownTile tile)> targets = unlockMgr.GetUnlockedTowns(excludeNodeId: currentNodeId);
                if (targets.Count == 0)
                {
                    DebugTools.Log("[TownUIController] 没有其他已解锁城镇，无法传送。");
                    yield break;
                }

                var menu = UIManager.Instance != null ? UIManager.Instance.TreasureMenuInstance : null;
                if (menu == null)
                {
                    DebugTools.LogWarning("[TownUIController] TreasureMenuInstance 未就绪，传送中止。");
                    yield break;
                }

                // ② 淡出城镇菜单（防止遮挡传送选单），并禁用交互。
                if (canvas != null)
                {
                    canvas.interactable = false;
                    canvas.blocksRaycasts = false;
                    canvas.DOKill();
                    canvas.DOFade(0f, 0.15f);
                }
                // 把传送选单移到 UI 层最顶层，确保渲染在最前。
                UIManager.Instance.TreasureMenuInstance.transform.SetAsLastSibling();
                yield return new WaitForSeconds(0.15f);

                // ③ 注册字段级委托（OnDisable 与 finally 双重兜底清理）。
                _teleportNodeSelected = false;
                _teleportCancelled = false;
                _teleportTargetNodeId = -1;

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

                // ④ 构建条目并展示。
                var items = new List<SimpleMenuItem>(targets.Count);
                foreach (var (nodeId, tile) in targets)
                {
                    items.Add(new SimpleMenuItem { Id = nodeId.ToString(), DisplayText = tile != null ? tile.townName : $"城镇 #{nodeId}" });
                }
                menu.ShowSimple(items);

                // ⑤ 等待玩家选择或取消。
                while (!_teleportNodeSelected && !_teleportCancelled)
                    yield return null;

                // ⑥ 取消路径：将城镇菜单淡回来并恢复交互。
                if (_teleportCancelled)
                {
                    if (canvas != null)
                    {
                        canvas.DOKill();
                        canvas.DOFade(1f, 0.15f);
                    }
                    yield return new WaitForSeconds(0.15f);
                    if (canvas != null)
                    {
                        canvas.interactable = true;
                        canvas.blocksRaycasts = true;
                    }
                    yield break;
                }

                // ⑦ 找到目标 TownTile。
                TownTile targetTile = null;
                foreach (var (nodeId, tile) in targets)
                {
                    if (nodeId == _teleportTargetNodeId) { targetTile = tile; break; }
                }

                // ⑧ 黑屏淡入。
                float fadeDuration = 1f;
                EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
                yield return new WaitForSeconds(fadeDuration);

                // ⑨ 移动玩家到目标节点。
                var board = BoardGameManager.Instance;
                if (board != null)
                {
                    board.movementController?.SetCurrentNodeById(_teleportTargetNodeId);
                }

                // ⑩ 同步相机。
                if (CameraManager.Instance != null && GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
                {
                    CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
                    CameraManager.Instance.WarpCameraToTarget();
                }

                // ⑪ 在黑屏期间热更新城镇数据（背景图切换）。
                if (view != null)
                {
                    view.Configure(_teleportTargetNodeId, targetTile);
                }

                // ⑫ 在黑屏内重新初始化按钮并恢复交互。
                if (view != null)
                {
                    view.RebuildMenuForOverlay();
                }
                if (canvas != null)
                {
                    canvas.blocksRaycasts = true;
                    canvas.interactable = true;
                    canvas.DOKill();
                    canvas.alpha = 0f;
                    canvas.DOFade(1f, 0.25f);
                }
                yield return new WaitForSeconds(0.25f);

                // ⑬ 黑屏淡出，目标城镇菜单呈现。
                EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });

                DebugTools.Log($"[TownUIController] 传送完成 → {targetTile?.townName ?? $"节点 {_teleportTargetNodeId}"}");
            }
            finally
            {
                // 任何退出路径都执行：清空互斥引用 + 兜底清理字段级事件委托。
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
