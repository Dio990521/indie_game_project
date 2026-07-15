using System;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.UI
{
    /// <summary>
    /// 棋盘操作菜单的业务分发器：
    /// 把"菜单项确认后触发什么业务"从 BoardActionMenuView 中迁出（原 OnButtonClick 的 switch）。
    ///
    /// 拆分动机（MVB 边界修复）：
    /// - View 应只负责显示/布局/输入选择，不应直接调用 BoardGameManager.ChangeState 这类业务 API；
    /// - 业务分发集中在此处后，新增菜单项只需要在这里加 case（以及 PlayerTurnState 的菜单数据），
    ///   View 完全不用改。
    ///
    /// 设计说明：
    /// - 无状态静态类：所有依赖（露营地 LocationID、隐藏菜单回调）由调用方传入；
    /// - hideMenu 回调由分发器决定调用时机——例如"宝具"必须先隐藏菜单再广播事件，
    ///   确保 TreasureMenu 的恢复逻辑能正常触发 Show（时序约定见 case 内注释）。
    /// </summary>
    public static class BoardActionDispatcher
    {
        /// <summary>
        /// 执行指定菜单项对应的业务。
        /// </summary>
        /// <param name="id">被确认的菜单项 ID。</param>
        /// <param name="campingLocationId">露营场景的 LocationID（Camp 项使用，允许为 null，缺失时打警告）。</param>
        /// <param name="hideMenu">隐藏操作菜单的回调（由 View 提供，分发器决定是否/何时调用）。</param>
        public static void Dispatch(BoardActionId id, LocationID campingLocationId, Action hideMenu)
        {
            switch (id)
            {
                case BoardActionId.RollDice:
                    EventBus.Raise(new BoardRollDiceRequestedEvent());
                    hideMenu?.Invoke();
                    break;

                case BoardActionId.Item:
                    EventBus.Raise(new OpenInventoryEvent());
                    hideMenu?.Invoke();
                    break;

                case BoardActionId.Treasure:
                    // 时序约定：先隐藏操作菜单，再广播请求，
                    // 确保 _isVisible=false 时宝具菜单取消后的"恢复操作菜单"逻辑可正常触发 Show。
                    hideMenu?.Invoke();
                    EventBus.Raise(new BoardTreasureMenuRequestedEvent());
                    break;

                case BoardActionId.Camp:
                    if (campingLocationId == null)
                    {
                        DebugTools.LogWarning("[BoardActionDispatcher] Missing campingLocationId.");
                        break;
                    }
                    if (BoardGameManager.Instance != null)
                    {
                        // 不主动隐藏菜单：切换到 CampingState 后由 PlayerTurnState.OnExit 统一隐藏
                        BoardGameManager.Instance.ChangeState(new CampingState(campingLocationId));
                    }
                    break;

                case BoardActionId.Map:
                    // TODO: 地图功能尚未实现，先用日志占位点击效果
                    DebugTools.Log("<color=cyan>[操作菜单] 点击了【地图】按钮（功能待实现）。</color>");
                    break;

                case BoardActionId.Equip:
                    EventBus.Raise(new OpenEquipmentUIEvent());
                    hideMenu?.Invoke();
                    break;
            }
        }
    }
}
