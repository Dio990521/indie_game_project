using UnityEngine;
using UnityEngine.Localization;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 宝具数据基类：所有宝具共享的通用字段定义。
    /// 具体宝具继承此类并添加专属字段。
    /// </summary>
    public abstract class TreasureSO : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("全局唯一 ID，与 TreasureItemSelectedEvent.TreasureId 对应")]
        public string TreasureId;

        [Header("UI 显示")]
        [Tooltip("本地化显示名称，显示在宝具列表每行的中间")]
        public LocalizedString DisplayName;

        [Tooltip("显示在列表左侧的宝具图标")]
        public Sprite Icon;

        [Header("规则")]
        [Tooltip("使用此宝具消耗的行动点数量")]
        public int ActionPointCost = 1;

        /// <summary>
        /// 创建本宝具的棋盘激活状态（M10 修复）：
        /// 由各宝具子类返回自己的激活状态实例，PlayerTurnState 只做一行多态调用，
        /// 取代原先"if-else 按 SO 类型逐个分发"的写法——新增宝具时只需新建
        /// SO 子类 + 状态类，不再需要修改 PlayerTurnState（开闭原则）。
        /// 返回 null 表示该宝具没有对应的激活状态（调用方回退到操作菜单）。
        /// </summary>
        public abstract BaseState<BoardGameManager> CreateActivationState();
    }
}
