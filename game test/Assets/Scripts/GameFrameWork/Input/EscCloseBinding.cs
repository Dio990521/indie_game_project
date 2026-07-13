using System;

namespace IndieGame.Core.Input
{
    /// <summary>
    /// 封装 UI 面板"ESC/手柄 Cancel 关闭"的订阅样板：
    /// 统一处理 GameInputReader.UICancelEvent 的订阅/退订、可见性判断与关闭回调触发。
    /// 用法：Controller 在 Awake 中 new 一个实例（传入可见性判断与关闭回调），
    /// 在 OnEnable/OnDisable 中分别调用 Subscribe()/Unsubscribe() 即可，无需再各自手写事件处理器。
    /// </summary>
    public sealed class EscCloseBinding
    {
        private readonly GameInputReader _inputReader;
        private readonly Func<bool> _isVisible;
        private readonly Action _onClose;

        public EscCloseBinding(GameInputReader inputReader, Func<bool> isVisible, Action onClose)
        {
            _inputReader = inputReader;
            _isVisible = isVisible;
            _onClose = onClose;
        }

        public void Subscribe()
        {
            if (_inputReader != null) _inputReader.UICancelEvent += HandleUICancel;
        }

        public void Unsubscribe()
        {
            if (_inputReader != null) _inputReader.UICancelEvent -= HandleUICancel;
        }

        private void HandleUICancel()
        {
            if (_isVisible != null && !_isVisible()) return;
            _onClose?.Invoke();
        }
    }
}
