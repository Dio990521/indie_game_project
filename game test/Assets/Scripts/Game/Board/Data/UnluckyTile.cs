using System;
using System.Collections.Generic;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 厄运格的效果类型
    /// </summary>
    public enum UnluckyEffectType
    {
        LoseGold,           // 失去金币
        LoseHP,             // 扣除生命值
        LoseItem,           // 失去随机道具
        MoveBack,           // 后退步数
        SkipNextTurn,       // 跳过下一回合
        SwapPosition,       // 与随机玩家交换位置
    }

    /// <summary>
    /// 厄运效果池中的单条配置
    /// </summary>
    [Serializable]
    public class UnluckyEffectEntry : IWeighted
    {
        [Tooltip("厄运效果类型")]
        public UnluckyEffectType effectType = UnluckyEffectType.LoseGold;

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
    /// 厄运格：玩家停下后从效果池中按权重随机触发一种负面效果。
    /// 所有效果当前以 Log 占位，待系统完善后替换为真实调用。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Unlucky Tile")]
    public class UnluckyTile : TileBase
    {
        [Header("厄运效果池")]
        [Tooltip("为该格子配置的负面效果池，按权重随机抽取一条")]
        [SerializeField] private List<UnluckyEffectEntry> effectPool = new();

        public override void OnPlayerStop(GameObject player)
        {
            // 过滤无效条目
            var validEntries = new List<UnluckyEffectEntry>();
            foreach (var entry in effectPool)
            {
                if (entry.IsValid())
                    validEntries.Add(entry);
            }

            if (validEntries.Count == 0)
            {
                DebugTools.LogWarning($"<color=orange>[Unlucky Tile]</color> 格子 \"{tileName}\" 的效果池为空或全部无效，跳过效果。");
                return;
            }

            // 按权重随机抽取一条效果
            UnluckyEffectEntry selected = WeightedRandomUtil.Pick(validEntries);
            if (selected == null) return;

            int value = UnityEngine.Random.Range(selected.minValue, selected.maxValue + 1);
            ApplyEffect(player, selected.effectType, value);
        }

        /// <summary>
        /// 执行对应的厄运效果（当前用 Log 占位）
        /// </summary>
        private void ApplyEffect(GameObject player, UnluckyEffectType effectType, int value)
        {
            switch (effectType)
            {
                case UnluckyEffectType.LoseGold:
                    // TODO: GoldSystem.Instance.SpendGold(value, "UnluckyTile");
                    DebugTools.Log($"<color=red>[Unlucky Tile]</color> 玩家 {player.name} 触发【厄运-失去金币】：-{value} 金币（待接入 GoldSystem）。");
                    break;

                case UnluckyEffectType.LoseHP:
                    // TODO: stats.TakeDamage(value);
                    DebugTools.Log($"<color=red>[Unlucky Tile]</color> 玩家 {player.name} 触发【厄运-扣除生命】：-{value} HP（待接入 CharacterStats）。");
                    break;

                case UnluckyEffectType.LoseItem:
                    // TODO: InventoryManager.Instance.RemoveRandomItem(value);
                    DebugTools.Log($"<color=red>[Unlucky Tile]</color> 玩家 {player.name} 触发【厄运-失去道具】：随机道具 x{value} 被移除（待接入 InventoryManager）。");
                    break;

                case UnluckyEffectType.MoveBack:
                    // TODO: BoardManager.Instance.MovePlayer(player, -value);
                    DebugTools.Log($"<color=red>[Unlucky Tile]</color> 玩家 {player.name} 触发【厄运-后退】：后退 {value} 步（待接入 BoardManager）。");
                    break;

                case UnluckyEffectType.SkipNextTurn:
                    // TODO: TurnManager.Instance.SkipNextTurn(player);
                    DebugTools.Log($"<color=red>[Unlucky Tile]</color> 玩家 {player.name} 触发【厄运-跳过回合】：下一回合无法行动（待接入 TurnManager）。");
                    break;

                case UnluckyEffectType.SwapPosition:
                    // TODO: BoardManager.Instance.SwapWithRandomPlayer(player);
                    DebugTools.Log($"<color=red>[Unlucky Tile]</color> 玩家 {player.name} 触发【厄运-交换位置】：与随机玩家交换格子位置（待接入 BoardManager）。");
                    break;

                default:
                    DebugTools.LogWarning($"<color=orange>[Unlucky Tile]</color> 未处理的效果类型：{effectType}。");
                    break;
            }
        }
    }
}
