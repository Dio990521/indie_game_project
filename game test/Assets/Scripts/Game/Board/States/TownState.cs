using System;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.UI;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 城镇状态：玩家进入城镇后的棋盘状态。
    /// 负责显示城镇 UI，并在玩家点击"离开"后切回玩家回合。
    /// </summary>
    public class TownState : BoardState
    {
        private BoardGameManager _context;
        private Action<TownLeaveRequestedEvent> _onLeave;
        private Action<CloseShopUIRequestEvent> _onShopClosed;

        /// <summary>
        /// 进入城镇状态：显示城镇 UI 并等待玩家离开。
        /// </summary>
        public override void OnEnter(BoardGameManager context)
        {
            _context = context;

            // 显示城镇 UI
            var townUI = UIManager.Instance?.TownUIInstance;
            if (townUI != null)
            {
                townUI.Show();
            }
            else
            {
                DebugTools.LogWarning("[TownState] TownUIInstance 未配置，无法显示城镇菜单。");
            }

            // 订阅"离开城镇"事件
            _onLeave = _ => LeaveTown();
            EventBus.Subscribe(_onLeave);

            // 商店关闭后返回城镇菜单
            _onShopClosed = _ => ReturnToTownMenu();
            EventBus.Subscribe(_onShopClosed);
        }

        /// <summary>
        /// 退出城镇状态：隐藏城镇 UI，清理事件订阅。
        /// </summary>
        public override void OnExit(BoardGameManager context)
        {
            if (_onLeave != null)
            {
                EventBus.Unsubscribe(_onLeave);
                _onLeave = null;
            }

            if (_onShopClosed != null)
            {
                EventBus.Unsubscribe(_onShopClosed);
                _onShopClosed = null;
            }

            UIManager.Instance?.TownUIInstance?.Hide();
            _context = null;
        }

        /// <summary>
        /// 玩家点击"离开"：切回玩家回合，重新显示 Action Menu。
        /// </summary>
        private void LeaveTown()
        {
            _context?.ChangeState(new PlayerTurnState());
        }

        /// <summary>
        /// 商店关闭后重新显示城镇菜单。
        /// </summary>
        private void ReturnToTownMenu()
        {
            if (_context == null) return;
            UIManager.Instance?.TownUIInstance?.Show();
        }
    }
}
