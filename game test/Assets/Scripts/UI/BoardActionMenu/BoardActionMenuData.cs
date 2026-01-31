using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace IndieGame.UI
{
    /// <summary>
    /// 棋盘操作类型枚举：
    /// 表示菜单中每个按钮的逻辑含义。
    /// </summary>
    public enum BoardActionId
    {
        // 掷骰子
        RollDice,
        // 道具
        Item,
        // 营地/整备
        Camp
    }

    /// <summary>
    /// 单个操作按钮的数据结构：
    /// 包含逻辑 ID、显示名称与图标。
    /// </summary>
    public class BoardActionOptionData
    {
        // 操作类型标识
        public BoardActionId Id;
        // 显示名称（支持本地化）
        public LocalizedString Name;
        // 图标
        public Sprite Icon;
    }

    /// <summary>
    /// 操作菜单数据容器：
    /// 用于批量传递按钮配置。
    /// </summary>
    public class BoardActionMenuData
    {
        // 按钮配置列表
        public readonly List<BoardActionOptionData> Options = new List<BoardActionOptionData>();
    }
}
