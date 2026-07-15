using System.Collections;
using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 自动存档等待器（可复用协程工具，非 MonoBehaviour）：
    /// 封装"发起 AutoSaveRequestedEvent → 按 RequestId+Reason 匹配 AutoSaveCompletedEvent →
    /// 带超时等待"的完整样板。
    ///
    /// 背景：该逻辑原先在 CampUIController（Sleep）与 TownUIController（Inn）中逐行对称地
    /// 各写了一份（静态序号 + 4 个 pending 字段 + 等待协程 + 匹配回调 + OnDisable 复位，
    /// 合计约 180 行重复）。抽取后新的接入方只需 3 行。
    ///
    /// 使用方法（宿主为 MonoBehaviour Controller）：
    /// <code>
    /// private readonly AutoSaveAwaiter _autoSave = new AutoSaveAwaiter("[CampUIController] Sleep");
    /// void OnEnable()  => _autoSave.Subscribe();
    /// void OnDisable() => _autoSave.Unsubscribe(); // 内部自动复位 pending 状态
    /// // 业务协程内：
    /// yield return _autoSave.RequestAndWait(AutoSaveReason.Sleep, slotIndex, note, timeoutSeconds);
    /// // 需要时可读取 _autoSave.LastCompleted / LastSuccess / LastError 做分支
    /// </code>
    ///
    /// 线程/生命周期约定：
    /// - 所有调用都在主线程；
    /// - 同一个 Awaiter 实例同一时刻只支持一个等待中的请求（宿主 Controller 本身有流程互斥）；
    /// - 宿主协程被 StopCoroutine 打断时，Unsubscribe() 会复位 pending，避免旧 RequestId 错误匹配。
    /// </summary>
    public class AutoSaveAwaiter
    {
        // 全局递增请求序号：跨所有 Awaiter 实例共享，保证 RequestId 全局唯一，
        // AutoSaveCompletedEvent 匹配时不会与其他来源串线。
        private static int _requestSerial;

        // 日志前缀（如 "[CampUIController] Sleep"），用于定位是哪条业务的存档出了问题
        private readonly string _logContext;

        // 当前等待中的请求 ID（-1 = 没有等待中的请求）
        private int _pendingRequestId = -1;
        // 当前等待中的请求来源（与 RequestId 双重匹配）
        private AutoSaveReason _pendingReason;
        // 是否已订阅完成事件
        private bool _isSubscribed;
        // 本次请求是否已收到完成回调（协程轮询标志）
        private bool _received;

        /// <summary> 最近一次请求是否收到了完成回调（false = 被跳过或超时）。 </summary>
        public bool LastCompleted { get; private set; }
        /// <summary> 最近一次请求是否保存成功（LastCompleted 为 true 时才有意义）。 </summary>
        public bool LastSuccess { get; private set; }
        /// <summary> 最近一次请求的错误信息（成功时为空）。 </summary>
        public string LastError { get; private set; }

        public AutoSaveAwaiter(string logContext)
        {
            _logContext = string.IsNullOrWhiteSpace(logContext) ? "[AutoSaveAwaiter]" : logContext;
        }

        /// <summary>
        /// 订阅完成事件（宿主 OnEnable 调用，幂等）。
        /// </summary>
        public void Subscribe()
        {
            if (_isSubscribed) return;
            EventBus.Subscribe<AutoSaveCompletedEvent>(HandleCompleted);
            _isSubscribed = true;
        }

        /// <summary>
        /// 退订完成事件并复位等待状态（宿主 OnDisable 调用，幂等）。
        /// 复位可防止宿主协程被 StopCoroutine 打断后，残留的旧 RequestId 在下次流程中错误匹配。
        /// </summary>
        public void Unsubscribe()
        {
            if (_isSubscribed)
            {
                EventBus.Unsubscribe<AutoSaveCompletedEvent>(HandleCompleted);
                _isSubscribed = false;
            }
            _pendingRequestId = -1;
            _received = false;
        }

        /// <summary>
        /// 发起自动存档请求并等待完成（带超时保护）：
        /// - 无 AutoSaveRequestedEvent 订阅方（AutoSaveService 未启用）：打警告后直接返回；
        /// - 超时：打警告后返回，业务流程继续，避免卡死在黑屏；
        /// - 失败：打警告后返回（结果可通过 LastSuccess/LastError 读取）。
        /// 全程使用 unscaled 时间，不受 timeScale（暂停）影响。
        /// </summary>
        /// <param name="reason">自动存档来源（用于槽位策略与回调匹配）。</param>
        /// <param name="slotIndex">目标槽位（&lt;0 表示交由 AutoSaveService 按策略决定）。</param>
        /// <param name="note">存档备注（可空，由 AutoSaveService 按 Reason 生成默认值）。</param>
        /// <param name="timeoutSeconds">等待超时（秒），内部下限 0.1s。</param>
        public IEnumerator RequestAndWait(AutoSaveReason reason, int slotIndex, string note, float timeoutSeconds)
        {
            LastCompleted = false;
            LastSuccess = false;
            LastError = null;

            if (!EventBus.HasSubscribers<AutoSaveRequestedEvent>())
            {
                DebugTools.LogWarning($"{_logContext} auto-save skipped: no AutoSaveRequestedEvent subscriber.");
                yield break;
            }

            _received = false;
            _pendingReason = reason;
            _pendingRequestId = ++_requestSerial;

            EventBus.Raise(new AutoSaveRequestedEvent
            {
                RequestId = _pendingRequestId,
                Reason = reason,
                SlotIndex = slotIndex,
                Note = note,
                // 调用方都是"黑屏期间等待存档结果再继续"的流程
                WaitForCompletion = true
            });

            float elapsed = 0f;
            float timeout = Mathf.Max(0.1f, timeoutSeconds);
            while (!_received && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_received)
            {
                DebugTools.LogWarning($"{_logContext} auto-save timed out after {timeout}s. Flow continues.");
            }
            else if (!LastSuccess)
            {
                DebugTools.LogWarning($"{_logContext} auto-save failed: {LastError}");
            }

            _pendingRequestId = -1;
        }

        /// <summary>
        /// 完成事件回调：仅处理"当前等待中的 RequestId + Reason"，忽略其他来源的存档结果。
        /// </summary>
        private void HandleCompleted(AutoSaveCompletedEvent evt)
        {
            if (_pendingRequestId < 0) return;
            if (evt.RequestId != _pendingRequestId) return;
            if (evt.Reason != _pendingReason) return;

            _received = true;
            LastCompleted = true;
            LastSuccess = evt.Success;
            LastError = evt.Error;
        }
    }
}
