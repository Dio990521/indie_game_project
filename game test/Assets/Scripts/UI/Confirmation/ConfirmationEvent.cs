using System;

namespace IndieGame.UI.Confirmation
{
    public struct ConfirmationRequest
    {
        public string Message;
        public Action OnConfirm;
        public Action OnCancel;
    }

    public static class ConfirmationEvent
    {
        public static event Action<ConfirmationRequest> OnRequested;
        public static event Action<bool> OnResponded;

        public static bool HasPending => _hasPending;
        private static bool _hasPending;
        private static ConfirmationRequest _pending;

        public static void Request(ConfirmationRequest request)
        {
            _pending = request;
            _hasPending = true;
            OnRequested?.Invoke(request);
        }

        public static void Respond(bool confirmed)
        {
            if (!_hasPending) return;
            _hasPending = false;
            OnResponded?.Invoke(confirmed);
            if (confirmed) _pending.OnConfirm?.Invoke();
            else _pending.OnCancel?.Invoke();
            _pending = default;
        }
    }
}
