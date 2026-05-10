using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.UI.Confirmation;

namespace IndieGame.Gameplay.Board.Events
{
    /// <summary>
    /// 路途经过采集点时触发的棋盘事件。
    /// 流程：朝向采集物 → 检查行动点 → 弹出"是否采集"选择 → 若确认则消耗行动点并随机获得物品。
    /// 无论选择如何，事件结束后移动控制器自动继续剩余步数。
    /// 在 WaypointConnection.events 中配置：progressPoint=采集物在曲线上的位置比例，contextTarget=采集物的Transform。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Board/Events/Gathering Point")]
    public class GatheringPointBoardEvent : BoardEventSO
    {
        [Header("交互设置")]
        [SerializeField] private float lookAtDuration = 0.5f;
        [SerializeField] private int actionPointCost = 1;
        // 留空则自动生成 "是否采集？（消耗 N 行动点）"
        [SerializeField] private string confirmMessage;

        [Header("采集物品表")]
        [SerializeField] private List<GatheringLootEntry> lootTable;

        public override IEnumerator Execute(BoardGameManager manager, Transform gatheringTransform)
        {
            // 1. 玩家朝向采集物
            yield return LookAt(manager.movementController.playerToken, gatheringTransform, lookAtDuration);

            // 2. 物品表为空则静默结束
            if (lootTable == null || lootTable.Count == 0) yield break;

            // 3. 行动点不足则静默跳过，玩家继续移动
            if (!ActionPointSystem.Instance.CanConsume(actionPointCost)) yield break;

            // 4. 弹出是否采集的确认框
            string message = string.IsNullOrEmpty(confirmMessage)
                ? $"是否采集？（消耗 {actionPointCost} 行动点）"
                : confirmMessage;

            bool choiceMade = false;
            bool doGather = false;
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message   = message,
                OnConfirm = () => { doGather = true; choiceMade = true; },
                OnCancel  = () => { choiceMade = true; }
            });
            yield return new WaitUntil(() => choiceMade);

            // 5. 选择 No 则直接继续行进剩余步数
            if (!doGather) yield break;

            // 6. 消耗行动点
            ActionPointSystem.Instance.TryConsumeActionPoints(actionPointCost, "Gathering");

            // 7. 加权随机选出采集物并给予玩家
            GatheringLootEntry selected = GetWeightedRandom();
            if (selected == null || selected.item == null) yield break;

            int amount = UnityEngine.Random.Range(selected.minAmount, selected.maxAmount + 1);
            InventoryManager.Instance.AddItem(selected.item, amount);

            // Execute() 返回后，MoveAlongConnection 协程自动恢复剩余步数的移动
        }

        /// <summary>
        /// 按 weight 字段进行加权随机选择，返回命中的条目。
        /// </summary>
        private GatheringLootEntry GetWeightedRandom()
        {
            float totalWeight = 0f;
            foreach (var entry in lootTable)
                totalWeight += entry.weight;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (var entry in lootTable)
            {
                cumulative += entry.weight;
                if (roll <= cumulative)
                    return entry;
            }
            // 浮点误差兜底
            return lootTable[lootTable.Count - 1];
        }

    }

    /// <summary>
    /// 采集物品表的单条配置项。
    /// </summary>
    [Serializable]
    public class GatheringLootEntry
    {
        public ItemSO item;
        public int minAmount = 1;
        public int maxAmount = 3;
        [Range(0f, 1f)] public float weight = 1f;
    }
}
