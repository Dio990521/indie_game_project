using System;
using UnityEngine;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 背包槽位：
    /// 用于存放具体道具与数量。
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        // 槽位里的道具
        public ItemSO Item;
        // 当前数量
        public int Count;

        public InventorySlot(ItemSO item, int count)
        {
            Item = item;
            Count = Mathf.Max(0, count);
        }
    }
}
