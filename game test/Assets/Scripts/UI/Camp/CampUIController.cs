using System.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 控制器（Controller / Manager）：
    /// <para>
    /// 负责"露营 → 睡觉"流程的业务编排：黑屏 → 恢复行动点 → 推进日期 → 自动存档 →
    /// 隐藏菜单 → 返回棋盘 → 黑屏淡出。同时管理流程互斥与自动存档的请求/响应匹配。
    /// </para>
    /// <para>
    /// MVB 边界说明：
    /// - View 仅负责显示/隐藏菜单与转发按钮点击事件（Sleep 转发为 CampSleepRequestedEvent）；
    /// - Controller（本类）负责所有跨系统编排（ActionPointSystem / DateSystem / SceneLoader / AutoSaveService）；
    /// - View 的引用通过 SerializeField 注入，仅用于在流程结束时调用 Hide() 隐藏菜单。
    /// </para>
    /// </summary>
    public class CampUIController : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("受控的 CampUIView 引用，流程末尾会调用其 Hide() 隐藏菜单。")]
        [SerializeField] private CampUIView view;

        [Header("Sleep Auto Save")]
        [Tooltip("是否在执行 Sleep 时自动触发一次存档。")]
        [SerializeField] private bool enableSleepAutoSave = true;

        [Tooltip("Sleep 自动存档写入槽位。")]
        [SerializeField] private int sleepAutoSaveSlotIndex = 0;

        [Tooltip("Sleep 自动存档备注（用于标题读档列表识别该存档来源）。")]
        [SerializeField] private string sleepAutoSaveNote = "AutoSave-Sleep";

        [Tooltip("等待自动存档完成的超时时长（秒）。超时后会继续返回棋盘，避免流程卡死。")]
        [SerializeField] private float sleepAutoSaveTimeoutSeconds = 8f;

        // 自动存档请求递增序号（静态）：用于把"本次 Sleep 请求"与"完成事件"精准匹配。
        private static int _sleepAutoSaveRequestSerial;
        // 当前 Sleep 流程正在等待的 RequestId（-1 表示当前没有等待中的自动存档）。
        private int _pendingSleepAutoSaveRequestId = -1;
        // 当前等待请求是否已收到 AutoSaveCompletedEvent 回调。
        private bool _pendingSleepAutoSaveCompleted;
        // 当前等待请求成功标记。
        private bool _pendingSleepAutoSaveSuccess;
        // 当前等待请求错误信息。
        private string _pendingSleepAutoSaveError;

        // 当前 Sleep 协程引用（非 null 即代表流程正在进行）。
        // 用于防止玩家在黑屏淡入期间快速重复点击 Sleep 按钮触发并行流程。
        private Coroutine _sleepCoroutine;

        private void OnEnable()
        {
            // 订阅露营 Sleep 业务请求（由 View 在按钮点击后发布）。
            EventBus.Subscribe<CampSleepRequestedEvent>(HandleSleepRequested);
            // 订阅自动存档完成回调（用 RequestId 匹配本次请求）。
            EventBus.Subscribe<AutoSaveCompletedEvent>(HandleSleepAutoSaveCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CampSleepRequestedEvent>(HandleSleepRequested);
            EventBus.Unsubscribe<AutoSaveCompletedEvent>(HandleSleepAutoSaveCompleted);

            // 兜底：组件被禁用/销毁时若 Sleep 协程仍在跑，强制停止并清理状态，
            // 避免下次启用时残留的 _sleepCoroutine 引用让互斥保护误判。
            if (_sleepCoroutine != null)
            {
                StopCoroutine(_sleepCoroutine);
                _sleepCoroutine = null;
            }
            _pendingSleepAutoSaveRequestId = -1;
            _pendingSleepAutoSaveCompleted = false;
            _pendingSleepAutoSaveSuccess = false;
            _pendingSleepAutoSaveError = null;
        }

        /// <summary>
        /// 处理"睡觉"业务请求：
        /// 互斥检查通过后启动 SleepRoutine 协程。
        /// </summary>
        private void HandleSleepRequested(CampSleepRequestedEvent evt)
        {
            if (_sleepCoroutine != null)
            {
                DebugTools.LogWarning("[CampUIController] Sleep 流程已在进行中，忽略重复请求。");
                return;
            }
            _sleepCoroutine = StartCoroutine(SleepRoutine());
        }

        /// <summary>
        /// 睡觉流程协程：
        /// 1) 黑屏淡入；2) 恢复行动点 + 推进日期；3) 自动存档（带超时）；
        /// 4) 隐藏露营菜单；5) 返回棋盘；6) 黑屏淡出 + 通知 GameManager 结束加载。
        /// </summary>
        private IEnumerator SleepRoutine()
        {
            float fadeDuration = 1f;

            // 1) 黑屏淡入：通过 EventBus 通知全局淡入淡出 UI 负责执行。
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
            yield return new WaitForSeconds(fadeDuration);

            // 2) 睡觉结算：恢复全部行动点，推进游戏日期。
            ActionPointSystem.Instance?.RefillActionPoints("Sleep");
            IndieGame.Gameplay.Date.DateSystem.Instance?.AdvanceDay();

            // 3) 在返回棋盘前执行一次自动存档：
            //    通过 EventBus 请求全局 AutoSaveService 处理，Controller 不直接调用 SaveManager，
            //    以便存档策略集中由 AutoSaveService 维护。
            yield return RequestSleepAutoSaveRoutine();

            // 4) 关闭露营 UI（避免与棋盘 UI 叠加）。
            if (view != null)
            {
                view.Hide();
            }

            // 5) 返回棋盘（不重复触发淡入淡出，由本流程统一控制）。
            if (SceneLoader.Instance != null)
            {
                yield return SceneLoader.Instance.ReturnToBoardRoutine(false, fadeDuration, false);
            }

            // 6) 黑屏淡出（等待棋盘加载完成后执行）。
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndLoading();
            }

            // 流程正常结束：清空互斥标记，允许下一次 Sleep。
            // 协程被 OnDisable 中途 Stop 的情况由 OnDisable 兜底清理。
            _sleepCoroutine = null;
        }

        /// <summary>
        /// 请求并等待"睡觉自动存档"完成：
        /// - 若关闭自动存档：直接跳过；
        /// - 若无监听方：记录警告并跳过；
        /// - 若超时：记录警告并继续流程，避免卡死在黑屏。
        /// </summary>
        private IEnumerator RequestSleepAutoSaveRoutine()
        {
            if (!enableSleepAutoSave) yield break;

            if (!EventBus.HasSubscribers<AutoSaveRequestedEvent>())
            {
                DebugTools.LogWarning("[CampUIController] Sleep auto-save skipped: no AutoSaveRequestedEvent subscriber.");
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
            // 使用 unscaledDeltaTime 避免被时间缩放（如暂停）阻塞等待。
            while (!_pendingSleepAutoSaveCompleted && elapsed < Mathf.Max(0.1f, sleepAutoSaveTimeoutSeconds))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_pendingSleepAutoSaveCompleted)
            {
                DebugTools.LogWarning("[CampUIController] Sleep auto-save timed out. Continue return-to-board flow.");
            }
            else if (!_pendingSleepAutoSaveSuccess)
            {
                DebugTools.LogWarning($"[CampUIController] Sleep auto-save failed: {_pendingSleepAutoSaveError}");
            }

            _pendingSleepAutoSaveRequestId = -1;
        }

        /// <summary>
        /// 接收自动存档完成事件：
        /// 仅处理"当前 Sleep 流程等待中的 requestId"，避免其他系统的存档结果污染当前流程。
        /// </summary>
        private void HandleSleepAutoSaveCompleted(AutoSaveCompletedEvent evt)
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
