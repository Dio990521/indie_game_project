using System;
using UnityEngine;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Gameplay.Crafting
{
    /// <summary>
    /// 图纸材料需求条目：
    /// 用于描述“制造 1 次”时需要消耗哪种道具、消耗多少数量。
    /// </summary>
    [Serializable]
    public class BlueprintRequirement
    {
        [Header("需求内容")]
        [Tooltip("所需材料道具（来自背包 ItemSO）")]
        [SerializeField] private ItemSO item;

        [Tooltip("所需数量（最小 1）")]
        [SerializeField] private int amount = 1;

        /// <summary> 所需道具（只读暴露） </summary>
        public ItemSO Item => item;

        /// <summary>
        /// 所需数量（运行时自动兜底到 >= 1，避免配置错误导致逻辑异常）
        /// </summary>
        public int Amount => Mathf.Max(1, amount);
    }
}
