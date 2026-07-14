using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Dialogue;

namespace IndieGame.Core
{
    // 数值系统事件：生命/经验/等级、金币、行动点、日期、全局标志、技能树
    // （L5 重构：原 GameEvents.cs 单文件 1000+ 行，按领域拆分为 GameEvents.*.cs 多文件，
    // 命名空间与全部类型定义保持不变，纯文件级重组。）
    /// <summary>
    /// 生命值变化事件：
    /// 由 CharacterStats 在受到伤害/治疗时广播。
    /// </summary>
    public struct HealthChangedEvent
    {
        // 归属对象（通常是角色 GameObject）
        public GameObject Owner;
        // 当前生命
        public int Current;
        // 最大生命
        public int Max;
    }

    /// <summary>
    /// 死亡事件：
    /// 生命归零时触发。
    /// </summary>
    public struct DeathEvent
    {
        // 归属对象
        public GameObject Owner;
    }

    /// <summary>
    /// 等级变化事件：
    /// 升级或设置等级时触发。
    /// </summary>
    public struct LevelChangedEvent
    {
        // 归属对象
        public GameObject Owner;
        // 当前等级
        public int Level;
    }

    /// <summary>
    /// 经验变化事件：
    /// 获得经验或升级后触发。
    /// </summary>
    public struct ExpChangedEvent
    {
        // 归属对象
        public GameObject Owner;
        // 当前经验
        public int Current;
        // 下一级所需经验
        public int Required;
    }
    /// <summary>
    /// 金币变更事件：
    /// 由 GoldSystem 在金币数值发生变化后广播，供 UI、商店、音效等系统监听。
    ///
    /// 设计说明：
    /// 1) 事件只表达“结果状态”，不表达“如何处理”；
    /// 2) 监听方应只读使用，不应尝试反向修改 GoldSystem；
    /// 3) Delta 可为正（收入）或负（消费），便于 UI 做绿色/红色动画提示。
    /// </summary>
    public struct GoldChangedEvent
    {
        // 变化后的金币总量
        public int CurrentGold;
        // 本次变化量（正数=增加，负数=减少，0=仅同步）
        public int Delta;
        // 变化原因（可选：如 "QuestReward" / "ShopPurchase" / "LoadRestore"）
        public string Reason;
    }

    /// <summary>
    /// 行动点变更事件：
    /// 由 ActionPointSystem 在行动点数值发生变化后广播，供 UI、日志、技能系统等监听。
    ///
    /// 设计说明：
    /// 1) Delta 为正表示恢复，为负表示消耗，为 0 表示仅同步（初始化/上限变更）；
    /// 2) MaxPoints 同步携带，UI 可直接渲染进度条，无需额外查询；
    /// 3) Reason 标明消耗/恢复来源，便于日志和未来的特效扩展。
    /// </summary>
    public struct ActionPointChangedEvent
    {
        // 变化后的剩余行动点
        public int CurrentPoints;
        // 当前行动点上限
        public int MaxPoints;
        // 本次变化量（正=恢复，负=消耗，0=仅同步）
        public int Delta;
        // 变化原因（如 "RollDice" / "SkillEffect" / "LoadRestore"）
        public string Reason;
    }
    /// <summary>
    /// 日期变更事件：
    /// DateSystem 在每次 Sleep/Inn 推进一天后广播，供 HUD、存档、特效等系统监听。
    /// </summary>
    public struct DateChangedEvent
    {
        // 当前年份
        public int Year;
        // 当前月份（1-12）
        public int Month;
        // 当前日（1-30）
        public int Day;
        // 已格式化的日期字符串，如 "第1年1月2日"
        public string FormattedDate;
    }

    /// <summary>
    /// 全局事件标志变更事件：
    /// GameFlagSystem 在某个 Flag 值发生变化时广播，供关卡障碍物、任务系统等响应式监听。
    /// </summary>
    public struct GameFlagChangedEvent
    {
        // 变更的 Flag 唯一标识（字符串 Key，如 "village_gate_opened"）
        public string Key;
        // 变更后的新值
        public bool NewValue;
    }
    /// <summary>
    /// 技能点变更事件：
    /// SkillTreeSystem 在 SP 数值发生变化后广播。
    /// Delta 为正 = 获得，为负 = 花费，为 0 = 仅同步（初始化/读档）。
    /// </summary>
    public struct SkillPointChangedEvent
    {
        // 变化后的当前 SP
        public int Current;
        // 本次变化量（正=获得，负=花费，0=仅同步）
        public int Delta;
    }

    /// <summary>
    /// 技能学习完成事件：
    /// SkillTreeSystem 在技能成功解锁后广播，供 UI 刷新与日志系统监听。
    /// </summary>
    public struct SkillLearnedEvent
    {
        // 已学习技能的唯一 ID
        public string SkillId;
    }

    /// <summary>
    /// 打开技能树界面请求事件：
    /// 由 HUD 按钮或外部业务入口发起，SkillTreeController 监听后执行显示逻辑。
    /// </summary>
    public struct OpenSkillTreeUIEvent { }

    /// <summary>
    /// 关闭技能树界面请求事件：
    /// 由关闭按钮或 ESC 发起，SkillTreeController 监听后执行隐藏逻辑。
    /// </summary>
    public struct CloseSkillTreeUIEvent { }
}
