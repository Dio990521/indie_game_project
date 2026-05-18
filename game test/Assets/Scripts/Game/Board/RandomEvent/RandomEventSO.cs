using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Inventory;
using UnityEngine;

namespace IndieGame.Gameplay.Board.RandomEvent
{
    /// <summary>
    /// 事件结果类型
    /// </summary>
    public enum EventRewardType
    {
        GainGold,         // 获得金币
        LoseGold,         // 扣除金币
        GainItem,         // 获得道具
        GainExp,          // 获得经验值
        GainActionPoint,  // 获得行动点
        LoseActionPoint,  // 扣除行动点
    }

    /// <summary>
    /// 单条事件结果配置
    /// </summary>
    [Serializable]
    public class EventReward
    {
        [Tooltip("结果类型")]
        public EventRewardType type = EventRewardType.GainGold;

        [Tooltip("道具引用（type = GainItem 时生效）")]
        public ItemSO item;

        [Tooltip("数量/金额/经验/行动点数值")]
        [Min(1)]
        public int amount = 10;
    }

    /// <summary>
    /// 单个随机事件的数据容器。
    /// 正面事件（isPositive = true）在高幸运值时更容易被抽中；
    /// 负面事件反之。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Events/Random Event")]
    public class RandomEventSO : ScriptableObject, IWeighted
    {
        [Header("基础信息")]
        [Tooltip("事件唯一标识，用于调试与未来存档扩展")]
        public string eventID;

        [Tooltip("事件标题，显示在确认框顶部")]
        public string eventTitle;

        [Tooltip("事件描述，向玩家说明发生了什么")]
        [TextArea(2, 5)]
        public string description;

        [Header("分类与权重")]
        [Tooltip("true = 正面事件（好结果），false = 负面事件（坏结果）")]
        public bool isPositive = true;

        [Tooltip("基础抽取权重，数值越大越容易被选中")]
        [Min(1)]
        public int weight = 1;

        [Header("结果列表")]
        [Tooltip("可配置多条结果，所有结果会同时生效")]
        public List<EventReward> rewards = new();

        /// <summary>
        /// IWeighted 接口实现，供 WeightedRandomUtil 使用
        /// </summary>
        public int Weight => weight;
    }
}
