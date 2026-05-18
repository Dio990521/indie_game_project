using System.Collections.Generic;
using System.Text;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.RandomEvent;
using IndieGame.Gameplay.Economy;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Stats;
using IndieGame.UI.Confirmation;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 事件格：玩家停下后从事件数据库中随机抽取一个事件并弹出确认框。
    /// 幸运值越高越容易抽到正面事件，反之越容易抽到负面事件。
    /// 确认后立即执行对应结果，事件效果强制生效（无法规避）。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Random Event Tile")]
    public class RandomEventTile : TileBase
    {
        [Header("事件数据库")]
        [Tooltip("该格子使用的随机事件数据库")]
        [SerializeField] private RandomEventDatabaseSO database;

        [Header("幸运值影响")]
        [Tooltip("幸运影响系数（0~1）。0 = 幸运值完全无效；1 = 幸运100时正面概率达90%")]
        [Range(0f, 1f)]
        [SerializeField] private float luckInfluence = 0.8f;

        [Tooltip("幸运值满值参考基准（与 CharacterStatConfigSO 中的最大幸运值保持一致）")]
        [Min(1)]
        [SerializeField] private float maxLuckReference = 100f;

        public override void OnPlayerStop(GameObject player)
        {
            if (database == null || database.events == null || database.events.Count == 0)
            {
                DebugTools.LogWarning($"<color=orange>[Event Tile]</color> 格子 \"{tileName}\" 未配置事件数据库或数据库为空，跳过。");
                return;
            }

            // 抽取事件
            RandomEventSO selectedEvent = PickEvent(player);
            if (selectedEvent == null)
            {
                DebugTools.LogWarning($"<color=orange>[Event Tile]</color> 格子 \"{tileName}\" 抽取失败（有效事件为空），跳过。");
                return;
            }

            // 构造确认框消息
            string message = BuildMessage(selectedEvent, player);

            // 弹出确认框，双回调均执行结果（事件强制触发）
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = message,
                OnConfirm = () => ApplyRewards(selectedEvent, player),
                OnCancel  = () => ApplyRewards(selectedEvent, player),
            });

            string tag = selectedEvent.isPositive ? "<color=cyan>[正面事件]</color>" : "<color=red>[负面事件]</color>";
            DebugTools.Log($"<color=yellow>[Event Tile]</color> 玩家 {player.name} 触发 {tag}：{selectedEvent.eventTitle}");
        }

        /// <summary>
        /// 两步法抽取事件：
        /// 1. 根据幸运值计算正面概率，决定抽正面池还是负面池
        /// 2. 在对应池内按 weight 加权随机抽取一条
        /// </summary>
        private RandomEventSO PickEvent(GameObject player)
        {
            // 读取幸运值
            float luck = 50f; // 默认中位值
            var stats = player.GetComponent<CharacterStats>();
            if (stats != null)
                luck = stats.Luck.Value;

            // 标准化幸运值到 [0,1]，再计算正面概率
            float normalizedLuck = Mathf.Clamp01(luck / maxLuckReference);
            float pPositive = Mathf.Clamp01(0.5f + (normalizedLuck - 0.5f) * luckInfluence);

            // 第一步：决定目标池（正面 or 负面）
            bool pickPositive = Random.value < pPositive;

            // 第二步：从目标池按权重抽取
            var targetPool = BuildPool(pickPositive);

            // 安全兜底：目标池为空时从全量池抽
            if (targetPool.Count == 0)
                targetPool = BuildPool(null);

            return targetPool.Count > 0 ? WeightedRandomUtil.Pick(targetPool) : null;
        }

        /// <summary>
        /// 构建事件池。
        /// isPositive = null 时返回全量有效事件列表。
        /// </summary>
        private List<RandomEventSO> BuildPool(bool? isPositive)
        {
            var pool = new List<RandomEventSO>();
            foreach (var evt in database.events)
            {
                if (evt == null || evt.Weight <= 0) continue;
                if (isPositive.HasValue && evt.isPositive != isPositive.Value) continue;
                pool.Add(evt);
            }
            return pool;
        }

        /// <summary>
        /// 构造确认框显示文本：标题 + 描述 + 结果列表
        /// </summary>
        private string BuildMessage(RandomEventSO evt, GameObject player)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"【{evt.eventTitle}】");
            sb.AppendLine("───────────────────");
            sb.AppendLine(evt.description);
            sb.AppendLine();
            sb.Append("结果：");
            sb.AppendLine(BuildRewardDescription(evt, player));
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 将结果列表转换为人类可读的文本，逐条以换行分隔。
        /// </summary>
        private string BuildRewardDescription(RandomEventSO evt, GameObject player)
        {
            if (evt.rewards == null || evt.rewards.Count == 0)
                return "无";

            var lines = new List<string>();
            foreach (var reward in evt.rewards)
            {
                switch (reward.type)
                {
                    case EventRewardType.GainGold:
                        lines.Add($"获得 {reward.amount} 金币");
                        break;
                    case EventRewardType.LoseGold:
                        lines.Add($"失去 {reward.amount} 金币");
                        break;
                    case EventRewardType.GainItem:
                        string itemName = reward.item != null ? reward.item.name : "（未配置道具）";
                        lines.Add($"获得 {itemName} ×{reward.amount}");
                        break;
                    case EventRewardType.GainExp:
                        lines.Add($"获得 {reward.amount} 经验值");
                        break;
                    case EventRewardType.GainActionPoint:
                        lines.Add($"获得 {reward.amount} 行动点");
                        break;
                    case EventRewardType.LoseActionPoint:
                        lines.Add($"失去 {reward.amount} 行动点");
                        break;
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 执行事件的所有结果。
        /// </summary>
        private void ApplyRewards(RandomEventSO evt, GameObject player)
        {
            if (evt.rewards == null) return;

            var stats = player.GetComponent<CharacterStats>();

            foreach (var reward in evt.rewards)
            {
                switch (reward.type)
                {
                    case EventRewardType.GainGold:
                        GoldSystem.Instance.AddGold(reward.amount, "RandomEvent");
                        break;

                    case EventRewardType.LoseGold:
                        GoldSystem.Instance.TrySpendGold(reward.amount, "RandomEvent");
                        break;

                    case EventRewardType.GainItem:
                        if (reward.item != null)
                        {
                            bool ok = InventoryManager.Instance.AddItem(reward.item, reward.amount, null);
                            if (!ok)
                                DebugTools.LogWarning($"<color=orange>[Event Tile]</color> 背包已满，无法给予 {reward.item.name} ×{reward.amount}。");
                        }
                        else
                        {
                            DebugTools.LogWarning($"<color=orange>[Event Tile]</color> 事件 \"{evt.eventID}\" 的 GainItem 结果未配置道具，跳过。");
                        }
                        break;

                    case EventRewardType.GainExp:
                        if (stats != null)
                            stats.AddExp(reward.amount);
                        else
                            DebugTools.LogWarning($"<color=orange>[Event Tile]</color> 玩家缺少 CharacterStats，无法给予经验值。");
                        break;

                    case EventRewardType.GainActionPoint:
                        ActionPointSystem.Instance.RestoreActionPoints(reward.amount, "RandomEvent");
                        break;

                    case EventRewardType.LoseActionPoint:
                        ActionPointSystem.Instance.TryConsumeActionPoints(reward.amount, "RandomEvent");
                        break;
                }
            }
        }
    }
}
