using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 全局自动存档服务（AutoSaveService）：
    /// 这是“自动存档能力”的统一逻辑入口，负责把各业务模块发来的 AutoSave 请求
    /// 转换为 SaveManager.SaveAsync 调用，并在完成后回传统一事件。
    ///
    /// 设计目标：
    /// 1) 统一入口：所有自动存档都走一套事件协议，避免各系统重复写 Save 调度代码；
    /// 2) 可扩展：通过 Reason + 槽位策略映射，为未来场景切换/战斗结束自动存档预留扩展位；
    /// 3) 可观测：通过 SourceTag 精准匹配 SaveCompletedEvent，避免并发时串回调；
    /// 4) 稳定性：内部采用串行队列，避免同一时刻多次写盘导致状态竞争。
    /// </summary>
    [DisallowMultipleComponent]
    public class AutoSaveService : MonoBehaviour
    {
        [Header("Default Policy")]
        [Tooltip("当请求未指定槽位（SlotIndex<0）且未命中来源策略时，自动存档写入的默认槽位。")]
        [SerializeField] private int defaultAutoSaveSlotIndex = 0;

        [Tooltip("自动备注前缀。请求未传入 Note 时，最终备注会拼接为 Prefix:Reason。")]
        [SerializeField] private string defaultNotePrefix = "AutoSave";

        [Header("Reason Slot Policy")]
        [Tooltip("按自动存档来源（Reason）覆盖默认槽位；同一 Reason 出现多条时，以最后一条为准。")]
        [SerializeField] private List<ReasonSlotPolicy> reasonSlotPolicies = new List<ReasonSlotPolicy>();

        // 待处理的自动存档请求队列（串行消费，避免并发写盘）。
        private readonly Queue<QueuedAutoSaveRequest> _requestQueue = new Queue<QueuedAutoSaveRequest>();
        // 当前是否正在消费队列。
        private bool _isProcessingQueue;
        // 当请求方未提供有效 RequestId 时，服务层自动生成的序列号。
        private int _generatedRequestSerial;

        private void OnEnable()
        {
            EventBus.Subscribe<AutoSaveRequestedEvent>(HandleAutoSaveRequestedEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AutoSaveRequestedEvent>(HandleAutoSaveRequestedEvent);
        }

        /// <summary>
        /// 接收自动存档请求并入队：
        /// 注意这里不直接执行 Save，统一交给队列处理，保证多个请求按顺序完成。
        /// </summary>
        private void HandleAutoSaveRequestedEvent(AutoSaveRequestedEvent evt)
        {
            QueuedAutoSaveRequest queuedRequest = BuildQueuedRequest(evt);
            _requestQueue.Enqueue(queuedRequest);

            if (_isProcessingQueue) return;
            _ = ProcessQueueAsync();
        }

        /// <summary>
        /// 串行消费自动存档队列。
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;

            try
            {
                while (_requestQueue.Count > 0)
                {
                    QueuedAutoSaveRequest request = _requestQueue.Dequeue();
                    await ExecuteSingleRequestAsync(request);
                }
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        /// <summary>
        /// 执行单条自动存档请求并发布完成事件。
        /// </summary>
        private async Task ExecuteSingleRequestAsync(QueuedAutoSaveRequest request)
        {
            SaveManager saveManager = FindAnyObjectByType<SaveManager>();
            if (saveManager == null)
            {
                PublishCompletedEvent(request, false, "SaveManager not found.");
                return;
            }

            // SourceTag 用于在 SaveCompletedEvent 回调里精准识别“本次请求”的保存结果。
            string sourceTag = $"AutoSaveService:{request.Reason}:{request.RequestId}";
            bool hasSaveCallback = false;
            bool saveSuccess = false;
            string saveError = null;

            void HandleSaveCompleted(SaveCompletedEvent evt)
            {
                // 双重匹配：
                // 1) SourceTag 必须相同，确保是本次请求触发的回调；
                // 2) 槽位必须一致，避免极端情况下错误匹配。
                if (!string.Equals(evt.SourceTag, sourceTag, StringComparison.Ordinal)) return;
                if (evt.SlotIndex != request.SlotIndex) return;

                hasSaveCallback = true;
                saveSuccess = evt.Success;
                saveError = evt.Error;
            }

            EventBus.Subscribe<SaveCompletedEvent>(HandleSaveCompleted);
            try
            {
                await saveManager.SaveAsync(request.SlotIndex, request.Note, sourceTag);
            }
            finally
            {
                EventBus.Unsubscribe<SaveCompletedEvent>(HandleSaveCompleted);
            }

            // 正常情况下 SaveManager.SaveAsync 一定会发布 SaveCompletedEvent。
            // 若未收到，说明链路异常（例如未来改动破坏约定），这里按失败处理并回传错误。
            if (!hasSaveCallback)
            {
                PublishCompletedEvent(request, false, "SaveCompletedEvent callback missing.");
                return;
            }

            PublishCompletedEvent(request, saveSuccess, saveError);
        }

        /// <summary>
        /// 组装队列请求：
        /// - 规范化 RequestId；
        /// - 路由最终槽位；
        /// - 生成最终 Note。
        /// </summary>
        private QueuedAutoSaveRequest BuildQueuedRequest(AutoSaveRequestedEvent evt)
        {
            int requestId = evt.RequestId > 0 ? evt.RequestId : ++_generatedRequestSerial;
            int slotIndex = ResolveSlotIndex(evt.Reason, evt.SlotIndex);
            string note = ResolveNote(evt.Reason, evt.Note);

            return new QueuedAutoSaveRequest
            {
                RequestId = requestId,
                Reason = evt.Reason,
                SlotIndex = slotIndex,
                Note = note,
                WaitForCompletion = evt.WaitForCompletion
            };
        }

        /// <summary>
        /// 解析最终槽位：
        /// 优先级从高到低：
        /// 1) 请求强制指定槽位（>=0）；
        /// 2) Reason 策略映射；
        /// 3) 默认槽位。
        /// </summary>
        private int ResolveSlotIndex(AutoSaveReason reason, int requestedSlotIndex)
        {
            if (requestedSlotIndex >= 0) return requestedSlotIndex;

            for (int i = 0; i < reasonSlotPolicies.Count; i++)
            {
                ReasonSlotPolicy policy = reasonSlotPolicies[i];
                if (policy == null) continue;
                if (policy.Reason != reason) continue;
                return Mathf.Max(0, policy.SlotIndex);
            }

            return Mathf.Max(0, defaultAutoSaveSlotIndex);
        }

        /// <summary>
        /// 解析最终备注：
        /// - 请求方有值时优先使用请求值；
        /// - 否则按统一格式生成默认备注，便于标题界面识别来源。
        /// </summary>
        private string ResolveNote(AutoSaveReason reason, string requestedNote)
        {
            if (!string.IsNullOrWhiteSpace(requestedNote))
            {
                return requestedNote.Trim();
            }

            string prefix = string.IsNullOrWhiteSpace(defaultNotePrefix) ? "AutoSave" : defaultNotePrefix.Trim();
            return $"{prefix}:{reason}";
        }

        /// <summary>
        /// 发布自动存档完成事件（统一出口，方便后续加入全局埋点）。
        /// </summary>
        private static void PublishCompletedEvent(QueuedAutoSaveRequest request, bool success, string error)
        {
            EventBus.Raise(new AutoSaveCompletedEvent
            {
                RequestId = request.RequestId,
                Reason = request.Reason,
                SlotIndex = request.SlotIndex,
                Success = success,
                Error = error
            });
        }

        /// <summary>
        /// Reason -> 槽位 策略配置项：
        /// 用于在 Inspector 中按来源配置默认槽位。
        /// </summary>
        [Serializable]
        private class ReasonSlotPolicy
        {
            public AutoSaveReason Reason = AutoSaveReason.None;
            public int SlotIndex = 0;
        }

        /// <summary>
        /// 队列中的内部请求结构：
        /// 与外部事件分离，目的是确保后续扩展内部字段时不影响事件协议稳定性。
        /// </summary>
        private struct QueuedAutoSaveRequest
        {
            public int RequestId;
            public AutoSaveReason Reason;
            public int SlotIndex;
            public string Note;
            public bool WaitForCompletion;
        }
    }
}
