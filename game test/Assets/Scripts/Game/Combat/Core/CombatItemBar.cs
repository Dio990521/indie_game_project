using System.Collections.Generic;
using IndieGame.Core;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗道具栏（纯 C#，由 CombatManager 持有）：
    /// 共 4 个"种类槽"——最多同时持有 4 种不同类型的道具，
    /// 每种道具按其 CarryLimit 独立堆叠；数量归零时释放槽位给新种类。
    /// 战斗开始为空、战斗结束清空（不进存档，不构成跨战斗资源积累）。
    /// 内容变化时广播 CombatItemBarChangedEvent 供 HUD 刷新。
    /// </summary>
    public class CombatItemBar
    {
        /// <summary> 种类槽上限 </summary>
        public const int MaxSlots = 4;

        /// <summary>
        /// 一个种类槽：某种道具及其持有数量。
        /// </summary>
        public class ItemStack
        {
            public CombatItemSO Item;
            public int Count;
        }

        private readonly List<ItemStack> _stacks = new List<ItemStack>(MaxSlots);

        /// <summary> 当前种类槽列表（顺序即 HUD 槽位顺序与数字键映射） </summary>
        public IReadOnlyList<ItemStack> Stacks => _stacks;

        /// <summary>
        /// 是否能接收一个该种道具：
        /// 已有同类槽 → 未达携带上限；无同类槽 → 还有空的种类槽。
        /// 生产系统据此判断"等待空槽"（进度条保持满值挂起）。
        /// </summary>
        public bool CanAccept(CombatItemSO item)
        {
            if (item == null) return false;
            ItemStack stack = FindStack(item);
            if (stack != null)
            {
                return stack.Count < UnityEngine.Mathf.Max(1, item.CarryLimit);
            }
            return _stacks.Count < MaxSlots;
        }

        /// <summary>
        /// 尝试放入一个道具（生产完成时调用）。
        /// </summary>
        /// <returns>true = 放入成功（已广播变更）</returns>
        public bool TryAdd(CombatItemSO item)
        {
            if (!CanAccept(item)) return false;

            ItemStack stack = FindStack(item);
            if (stack == null)
            {
                stack = new ItemStack { Item = item, Count = 0 };
                _stacks.Add(stack);
            }
            stack.Count++;
            Broadcast();
            return true;
        }

        /// <summary>
        /// 查看某槽位的道具类型（越界/空槽返回 null）。
        /// </summary>
        public CombatItemSO Peek(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _stacks.Count) return null;
            return _stacks[slotIndex].Item;
        }

        /// <summary>
        /// 按道具类型查槽位索引（未持有返回 -1）。
        /// </summary>
        public int FindSlotIndex(CombatItemSO item)
        {
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Item == item) return i;
            }
            return -1;
        }

        /// <summary>
        /// 消耗某槽位的一个道具（使用成功时调用）：
        /// 校验槽位当前类型与预期一致（防瞄准期间槽位位移导致误消耗）；数量归零则移除槽位。
        /// </summary>
        /// <returns>true = 消耗成功（已广播变更）</returns>
        public bool TryConsume(int slotIndex, CombatItemSO expectedItem)
        {
            if (slotIndex < 0 || slotIndex >= _stacks.Count) return false;
            ItemStack stack = _stacks[slotIndex];
            if (expectedItem != null && stack.Item != expectedItem) return false;
            if (stack.Count <= 0) return false;

            stack.Count--;
            if (stack.Count <= 0)
            {
                // 数量归零释放种类槽（"使用道具腾出空间"的设计语义）
                _stacks.RemoveAt(slotIndex);
            }
            Broadcast();
            return true;
        }

        /// <summary>
        /// 清空道具栏（战斗开始/结束/休眠时调用）。
        /// </summary>
        /// <param name="broadcast">是否广播变更（战斗外清理可跳过，避免向已隐藏的 HUD 发事件）</param>
        public void Clear(bool broadcast)
        {
            _stacks.Clear();
            if (broadcast) Broadcast();
        }

        private ItemStack FindStack(CombatItemSO item)
        {
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Item == item) return _stacks[i];
            }
            return null;
        }

        private void Broadcast()
        {
            EventBus.Raise(new CombatItemBarChangedEvent { Bar = this });
        }
    }
}
