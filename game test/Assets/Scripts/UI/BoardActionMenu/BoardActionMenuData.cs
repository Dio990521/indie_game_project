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
        Camp,
        // 宝具
        Treasure,
        // 地图
        Map,
        // 装备
        Equip
    }

    /// <summary>
    /// 按钮所在的侧别：
    /// 菜单以玩家为中心分为左右两侧圆弧展开，Side 决定按钮归属哪一侧。
    /// </summary>
    public enum BoardActionSide
    {
        Left,
        Right
    }

    /// <summary>
    /// 单个操作按钮的数据结构：
    /// 包含逻辑 ID、显示名称、图标与所属侧别。
    /// </summary>
    public class BoardActionOptionData
    {
        // 操作类型标识
        public BoardActionId Id;
        // 显示名称（支持本地化）
        public LocalizedString Name;
        // 图标
        public Sprite Icon;
        // 所属侧别（左侧/右侧），决定圆弧布局与方向键分组
        public BoardActionSide Side;
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
