using System;
using IndieGame.Core;

namespace IndieGame.UI.Confirmation
{
    public struct ConfirmationRequest
    {
        public string Message;
        public Action OnConfirm;
        public Action OnCancel;
    }

    public struct ConfirmationRequestEvent
    {
        public ConfirmationRequest Request;
    }

    public struct ConfirmationRespondedEvent
    {
        public bool Confirmed;
    }

    public static class ConfirmationEvent
    {
        public static bool HasPending => _hasPending;
        private static bool _hasPending;
        private static ConfirmationRequest _pending;

        public static void Request(ConfirmationRequest request)
        {
            _pending = request;
            _hasPending = true;
            EventBus.Raise(new ConfirmationRequestEvent { Request = request });
        }

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
