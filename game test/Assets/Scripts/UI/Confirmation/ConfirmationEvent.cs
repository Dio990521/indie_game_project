using System;
using IndieGame.Core;

namespace IndieGame.UI.Confirmation
{
    /// <summary>
    /// 确认请求结构：
    /// 包含提示文本与确认/取消回调。
    /// </summary>
    public struct ConfirmationRequest
    {
        // 提示文本
        public string Message;
        // 确认后的回调
        public Action OnConfirm;
        // 取消后的回调
        public Action OnCancel;
    }

    /// <summary>
    /// 确认弹窗请求事件：
    /// 发出该事件后，UI 层会显示弹窗。
    /// </summary>
    public struct ConfirmationRequestEvent
    {
        // 请求载荷
        public ConfirmationRequest Request;
    }

    /// <summary>
    /// 确认弹窗响应事件：
    /// UI 点击确认/取消后广播。
    /// </summary>
    public struct ConfirmationRespondedEvent
    {
        // true = 确认, false = 取消
        public bool Confirmed;
    }

    /// <summary>
    /// 确认弹窗事件入口：
    /// 通过静态方法 Request/Respond 管理弹窗流程与回调。
    /// </summary>
    public static class ConfirmationEvent
    {
        /// <summary>
        /// 当前是否存在未处理的请求。
        /// </summary>
        public static bool HasPending => _hasPending;
        // 是否存在待处理请求
        private static bool _hasPending;
        // 当前挂起的请求内容
        private static ConfirmationRequest _pending;

        /// <summary>
        /// 发起一个确认请求：
        /// 会记录请求并通过 EventBus 广播给 UI。
        /// </summary>
        public static void Request(ConfirmationRequest request)
        {
            _pending = request;
            _hasPending = true;
            EventBus.Raise(new ConfirmationRequestEvent { Request = request });
        }

        /// <summary>
        /// 响应确认弹窗：
        /// - 广播响应事件
        /// - 执行对应回调
        /// - 清空挂起请求
        /// </summary>
        public static void Respond(bool confirmed)
        {
            if (!_hasPending) return;
            _hasPending = false;
            EventBus.Raise(new ConfirmationRespondedEvent { Confirmed = confirmed });
            if (confirmed) _pending.OnConfirm?.Invoke();
            else _pending.OnCancel?.Invoke();
            _pending = default;
        }
    }
}
