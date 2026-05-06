using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;
using UnityEngine;


namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 幸运格的效果类型
    /// </summary>
    public enum LuckyEffectType
    {
        GainGold,           // 获得金币
        GainHP,             // 恢复生命值
        GainItem,           // 获得随机道具
        DoubleNextReward,   // 下次奖励翻倍
        ExtraMove,          // 额外移动步数
        SkipBadEvent,       // 免疫下一次负面事件
    }

    /// <summary>
    /// 幸运效果池中的单条配置
    /// </summary>
    [Serializable]
    public class LuckyEffectEntry : IWeighted
    {
        [Tooltip("幸运效果类型")]
        public LuckyEffectType effectType = LuckyEffectType.GainGold;

        [Tooltip("效果数值下限（含），适用于金币/HP/步数等数量类效果")]
        [Min(1)]
        public int minValue = 10;

        [Tooltip("效果数值上限（含），必须 >= minValue")]
        [Min(1)]
        public int maxValue = 50;

        [Tooltip("抽中该效果的相对权重，数值越大越容易抽中")]
        [Min(1)]
        public int weight = 1;

        // IWeighted 接口实现
        public int Weight => weight;

        /// <summary>
        /// 判断该条目配置是否有效
        /// </summary>
        public bool IsValid() => weight > 0 && minValue > 0 && maxValue >= minValue;
    }

    /// <summary>
    /// 幸运格：玩家停下后从效果池中按权重随机触发一种正面效果。
    /// 所有效果当前以 Log 占位，待系统完善后替换为真实调用。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Lucky Tile")]
    public class LuckyTile : TileBase
    {
        [Header("幸运效果池")]
        [Tooltip("为该格子配置的正面效果池，按权重随机抽取一条")]
        [SerializeField] private List<LuckyEffectEntry> effectPool = new();

        public override void OnPlayerStop(GameObject player)
        {
            // 过滤无效条目
            var validEntries = new List<LuckyEffectEntry>();
            foreach (var entry in effectPool)
            {
                if (entry.IsValid())
                    validEntries.Add(entry);
            }

            if (validEntries.Count == 0)
            {
                DebugTools.LogWarning($"<color=orange>[Lucky Tile]</color> 格子 \"{tileName}\" 的效果池为空或全部无效，跳过效果。");
                return;
            }

            // 按权重随机抽取一条效果
            LuckyEffectEntry selected = WeightedRandomUtil.Pick(validEntries);
            if (selected == null) return;

            int value = UnityEngine.Random.Range(selected.minValue, selected.maxValue + 1);
            ApplyEffect(player, selected.effectType, value);
        }

        /// <summary>
        /// 执行对应的幸运效果（当前用 Log 占位）
        /// </summary>
        private void ApplyEffect(GameObject player, LuckyEffectType effectType, int value)
        {
            switch (effectType)
            {
                case LuckyEffectType.GainGold:
                    // TODO: GoldSystem.Instance.AddGold(value, "LuckyTile");
                    DebugTools.Log($"<color=yellow>[Lucky Tile]</color> 玩家 {player.name} 触发【幸运-获得金币】：+{value} 金币（待接入 GoldSystem）。");
                    break;

                case LuckyEffectType.GainHP:
                    // TODO: stats.Heal(value);
                    DebugTools.Log($"<color=green>[Lucky Tile]</color> 玩家 {player.name} 触发【幸运-恢复生命】：+{value} HP（待接入 CharacterStats）。");
                    break;

                case LuckyEffectType.GainItem:
                    // TODO: InventoryManager.Instance.AddItem(randomItem, value, null);
                    DebugTools.Log($"<color=cyan>[Lucky Tile]</color> 玩家 {player.name} 触发【幸运-获得道具】：随机道具 x{value}（待接入 InventoryManager）。");
                    break;

                case LuckyEffectType.DoubleNextReward:
                    // TODO: 给玩家挂一个"下次奖励翻倍"的 Buff
                    DebugTools.Log($"<color=magenta>[Lucky Tile]</color> 玩家 {player.name} 触发【幸运-奖励翻倍】：下次获得的奖励翻倍（待接入 Buff 系统）。");
                    break;

                case LuckyEffectType.ExtraMove:
                    // TODO: BoardManager.Instance.MovePlayer(player, value);
                    DebugTools.Log($"<color=cyan>[Lucky Tile]</color> 玩家 {player.name} 触发【幸运-额外移动】：额外前进 {value} 步（待接入 BoardManager）。");
                    break;

                case LuckyEffectType.SkipBadEvent:
                    // TODO: 给玩家挂一个"免疫下次负面事件"的 Buff
                    DebugTools.Log($"<color=green>[Lucky Tile]</color> 玩家 {player.name} 触发【幸运-负面免疫】：下次负面事件无效（待接入 Buff 系统）。");
                    break;

                default:
                    DebugTools.LogWarning($"<color=orange>[Lucky Tile]</color> 未处理的效果类型：{effectType}。");
                    break;
            }
        }
    }
}
