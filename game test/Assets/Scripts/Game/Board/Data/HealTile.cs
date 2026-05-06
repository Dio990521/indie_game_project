using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Stats;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 治愈格：玩家停下后恢复全部 HP，并清除所有异常状态（异常状态系统待实现）。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Heal Tile")]
    public class HealTile : TileBase
    {
        public override void OnPlayerStop(GameObject player)
        {
            var stats = player.GetComponent<CharacterStats>();
            if (stats == null)
            {
                DebugTools.LogWarning($"<color=red>[Heal Tile]</color> 玩家 {player.name} 缺少 CharacterStats 组件，无法治愈。");
                return;
            }

            int healAmount = stats.MaxHP - stats.CurrentHP;

            // 回复全部生命值
            if (healAmount > 0)
            {
                stats.Heal(healAmount);
                DebugTools.Log($"<color=green>[Heal Tile]</color> 玩家 {player.name} 恢复了 <color=green>{healAmount} HP</color>，当前 HP：{stats.CurrentHP}/{stats.MaxHP}。");
            }
            else
            {
                DebugTools.Log($"<color=green>[Heal Tile]</color> 玩家 {player.name} HP 已满（{stats.CurrentHP}/{stats.MaxHP}），无需治愈。");
            }

            // TODO：异常状态系统未实现，此处占位
            DebugTools.Log($"<color=green>[Heal Tile]</color> 玩家 {player.name} 的所有异常状态已清除（待实现）。");
        }
    }
}
