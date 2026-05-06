using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Economy;
using IndieGame.Gameplay.Inventory;
using UnityEngine;


namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 奖励类型：金币或道具
    /// </summary>
    public enum SurpriseRewardType
    {
        Gold,
        Item
    }

    /// <summary>
    /// 奖励池中的单条奖励配置
    /// </summary>
    [Serializable]
    public class SurpriseRewardEntry : IWeighted
    {
        [Tooltip("奖励类型")]
        public SurpriseRewardType type = SurpriseRewardType.Gold;

        [Tooltip("道具引用（type = Item 时生效）")]
        public ItemSO item;

        [Tooltip("奖励数量下限（含）")]
        public int minAmount = 1;

        [Tooltip("奖励数量上限（含），必须 >= minAmount")]
        public int maxAmount = 10;

        [Tooltip("抽中该条目的相对权重，数值越大越容易抽中")]
        [Min(1)]
        public int weight = 1;

        // IWeighted 接口实现
        public int Weight => weight;

        /// <summary>
        /// 判断该条目配置是否有效
        /// </summary>
        public bool IsValid()
        {
            if (weight <= 0) return false;
            if (type == SurpriseRewardType.Item && item == null) return false;
            if (minAmount <= 0 || maxAmount < minAmount) return false;
            return true;
        }
    }

    /// <summary>
    /// 惊喜格：玩家停下后从奖励池中按权重随机抽取一项奖励（金币或道具），
    /// 每个格子实例可在 Inspector 中独立配置奖励库。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Surprise Tile")]
    public class SurpriseTile : TileBase
    {
        [Header("奖励库")]
        [Tooltip("为该格子单独配置的奖励池，支持金币与道具混合，按权重随机抽取一条")]
        [SerializeField] private List<SurpriseRewardEntry> rewardPool = new();

        public override void OnPlayerStop(GameObject player)
        {
            // 过滤无效条目
            var validEntries = new List<SurpriseRewardEntry>();
            foreach (var entry in rewardPool)
            {
                if (entry.IsValid())
                    validEntries.Add(entry);
            }

            if (validEntries.Count == 0)
            {
                DebugTools.LogWarning($"<color=orange>[Surprise Tile]</color> 格子 \"{tileName}\" 的奖励池为空或全部无效，跳过奖励。");
                return;
            }

            // 按权重随机选出一条
            SurpriseRewardEntry selected = WeightedRandomUtil.Pick(validEntries);
            if (selected == null) return;

            // 计算实际数量
            int amount = UnityEngine.Random.Range(selected.minAmount, selected.maxAmount + 1);

            // 发放奖励
            if (selected.type == SurpriseRewardType.Gold)
            {
                GoldSystem.Instance.AddGold(amount, "SurpriseTile");
                DebugTools.Log($"<color=yellow>[Surprise Tile]</color> 玩家 {player.name} 获得了 <color=yellow>{amount} 金币</color>！");
            }
            else
            {
                bool success = InventoryManager.Instance.AddItem(selected.item, amount, null);
                if (success)
                    DebugTools.Log($"<color=cyan>[Surprise Tile]</color> 玩家 {player.name} 获得了 <color=cyan>{selected.item.name} x{amount}</color>！");
                else
                    DebugTools.LogWarning($"<color=orange>[Surprise Tile]</color> 背包已满，无法给予 {selected.item.name} x{amount}。");
            }
        }
    }
}
