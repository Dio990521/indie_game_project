using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Core;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 背包管理器：作为单例存在，管理玩家的所有物品。
    /// 负责物品的存储、背包界面的开启与关闭，以及物品的使用逻辑分发。
    /// 继承自 MonoSingleton 以确保全局唯一访问。
    /// </summary>
    public class InventoryManager : MonoSingleton<InventoryManager>
    {
        // 覆盖单例属性：跨场景保留。
        // 注：原代码使用旧 DestroyOnLoad => true，由于基类语义反向 Bug，该值实际触发了
        // DontDestroyOnLoad，本管理器一直跨场景常驻。迁移到 KeepAcrossScenes 时保持实际
        // 运行时行为不变（保留），避免回归。
        protected override bool KeepAcrossScenes => true;

        [Header("数据")]
        [Tooltip("背包最大容量（槽位数）。")]
        public int maxCapacity = 30;

        [Tooltip("当前背包槽位列表。每个槽位包含道具与数量。")]
        public List<InventorySlot> slots = new List<InventorySlot>();

        // 通信方式说明：
        // - 内容变化  → EventBus.Raise(new OnInventoryChanged { Slots = slots });
        // - 已打开    → EventBus.Raise(new InventoryOpenedEvent());
        // - 已关闭    → EventBus.Raise(new InventoryClosedEvent());
        // 旧的静态委托（OnInventoryUpdated/OnInventoryOpened/OnInventoryClosed）已移除，
        // 历史订阅方应改为 EventBus.Subscribe<...> 形式（详见 P1-3 迁移说明）。

        // 初始化标记
        private bool _isInitialized;

        private void OnEnable()
        {
            // 通过事件总线订阅“打开背包”事件。
            // 这种解耦方式允许任何地方（如棋盘菜单或快捷键）发出 OpenInventoryEvent 即可打开背包。
            EventBus.Subscribe<OpenInventoryEvent>(HandleOpenInventory);
        }

        private void OnDisable()
        {
            // 禁用时务必取消订阅，防止内存泄漏或无效引用
            EventBus.Unsubscribe<OpenInventoryEvent>(HandleOpenInventory);
        }

        /// <summary>
        /// 响应事件总线的打开背包请求。
        /// </summary>
        private void HandleOpenInventory(OpenInventoryEvent evt)
        {
            OpenInventory();
        }

        /// <summary>
        /// 执行初始化逻辑。
        /// </summary>
        public void Init()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            // 初始化完成后主动广播一次背包快照，便于依赖系统（如打造 UI）即时同步按钮状态
            NotifyInventoryChanged();
        }

        /// <summary>
        /// 打开背包：通知 UI 层级同步数据并显示界面。
        /// </summary>
        public void OpenInventory()
        {
            // 1. 发送槽位数据同步事件，UI 将根据 slots 列表渲染
            NotifyInventoryChanged();
            // 2. 广播"已打开"状态通知，UI 据此显示界面/播放动画
            EventBus.Raise(new InventoryOpenedEvent());
        }

        /// <summary>
        /// 关闭背包：通知 UI 层级隐藏界面。
        /// </summary>
        public void CloseInventory()
        {
            EventBus.Raise(new InventoryClosedEvent());
        }

        /// <summary>
        /// 使用指定的物品。
        /// </summary>
        /// <param name="item">要使用的物品配置文件</param>
        public void UseItem(ItemSO item)
        {
            if (item == null) return;

            // 调用 ItemSO 中定义的具体使用逻辑。
            // 例如：加血、增加棋盘移动步数、或者触发特定的 BoardEvent。
            item.Use();

            // 消耗品使用后扣减数量
            if (item.Category == ItemCategory.Consumable)
            {
                RemoveItem(item, 1);
            }
        }

        /// <summary>
        /// 预测“是否可以完整加入指定数量的物品”：
        /// 该方法不会修改任何背包状态，仅用于交易前校验（例如商店购买）。
        ///
        /// 设计目的：
        /// 1) 让业务层先做“可完整放入”判断，避免发生“先扣钱后入包失败”；
        /// 2) 与 AddItem 形成“先判断再执行”的安全组合；
        /// 3) 保持与现有堆叠规则一致（同 ItemSO 且同 CustomName 才可堆叠）。
        /// </summary>
        /// <param name="item">目标物品</param>
        /// <param name="amount">目标数量（必须 >0）</param>
        /// <param name="customName">
        /// 实例名称（可选）：
        /// - 空字符串表示默认名；
        /// - 非空时必须与槽位 CustomName 完全一致才可堆叠。
        /// </param>
        /// <returns>true=可完整加入；false=空间不足或参数非法</returns>
        public bool CanAddItem(ItemSO item, int amount = 1, string customName = null)
        {
            if (item == null || amount <= 0) return false;

            int freeSlotCount = Mathf.Max(0, maxCapacity - slots.Count);
            string normalizedCustomName = string.IsNullOrWhiteSpace(customName) ? string.Empty : customName.Trim();

            // 非堆叠物品：
            // 每个数量都需要独立槽位，因此只要空槽数量 >= amount 即可。
            if (!item.isStackable)
            {
                return freeSlotCount >= amount;
            }

            // 堆叠物品：
            // 先统计“已有可堆叠槽位”的剩余空间，再计算还需要几个新槽位。
            int stackMax = Mathf.Max(1, item.maxStack);
            long remaining = amount;

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null || !slot.CanStackWith(item, normalizedCustomName)) continue;
                if (slot.Count >= stackMax) continue;

                int space = stackMax - Mathf.Max(0, slot.Count);
                if (space <= 0) continue;

                remaining -= space;
                if (remaining <= 0)
                {
                    return true;
                }
            }

            // 现有堆叠空间不够，需要新建槽位。
            // 需要槽位数 = ceil(remaining / stackMax)
            int requiredNewSlots = (int)((remaining + stackMax - 1L) / stackMax);
            return freeSlotCount >= requiredNewSlots;
        }

        /// <summary>
        /// 添加道具：
        /// - 先尝试堆叠（填满已有槽位）
        /// - 再尝试新建槽位
        /// - 超出容量时返回 false
        /// </summary>
        /// <param name="customName">
        /// 物品实例自定义名称：
        /// - null/空：使用道具原始名称（可与同类默认名称堆叠）
        /// - 非空：作为实例名写入槽位，仅与“同名同类”槽位堆叠
        /// </param>
        public bool AddItem(ItemSO item, int amount = 1, string customName = null)
        {
            if (item == null || amount <= 0) return false;

            int remaining = amount;
            // 先做一次标准化，避免循环里重复 Trim
            string normalizedCustomName = string.IsNullOrWhiteSpace(customName) ? string.Empty : customName.Trim();

            // 1) 尝试堆叠到已有槽位
            if (item.isStackable)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    InventorySlot slot = slots[i];
                    if (slot == null || !slot.CanStackWith(item, normalizedCustomName)) continue;
                    if (slot.Count >= item.maxStack) continue;

                    int space = Mathf.Max(0, item.maxStack - slot.Count);
                    int add = Mathf.Min(space, remaining);
                    slot.Count += add;
                    remaining -= add;
                    if (remaining <= 0) break;
                }
            }

            // 2) 新建槽位
            while (remaining > 0)
            {
                if (slots.Count >= maxCapacity)
                {
                    // 容量不足
                    NotifyInventoryChanged();
                    return false;
                }

                int addCount = item.isStackable ? Mathf.Min(item.maxStack, remaining) : 1;
                slots.Add(new InventorySlot(item, addCount, normalizedCustomName));
                remaining -= addCount;
            }

            NotifyInventoryChanged();
            return true;
        }

        /// <summary>
        /// 移除道具：
        /// 按数量扣减，扣完后清理空槽位。
        /// </summary>
        public bool RemoveItem(ItemSO item, int amount)
        {
            if (item == null || amount <= 0) return false;

            int remaining = amount;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                InventorySlot slot = slots[i];
                if (slot == null || slot.Item != item) continue;

                if (slot.Count > remaining)
                {
                    slot.Count -= remaining;
                    remaining = 0;
                    break;
                }
                else
                {
                    remaining -= slot.Count;
                    slots.RemoveAt(i);
                    if (remaining <= 0) break;
                }
            }

            NotifyInventoryChanged();
            return remaining == 0;
        }

        /// <summary>
        /// 按引用精确移除整个槽位（装备武器时使用）：
        /// 与 RemoveItem(ItemSO, amount) 的弱匹配不同，这里直接摘除槽位对象本身，
        /// 不会误删背包里另一把"同 WeaponSO 但强化数据不同"的武器实例。
        /// </summary>
        public bool RemoveSlot(InventorySlot slot)
        {
            if (slot == null) return false;
            bool removed = slots.Remove(slot);
            if (removed) NotifyInventoryChanged();
            return removed;
        }

        /// <summary>
        /// 校验是否还有空槽位（卸下武器前置判断）。
        /// </summary>
        public bool CanInsertSlot()
        {
            return slots.Count < maxCapacity;
        }

        /// <summary>
        /// 把一个槽位对象原样插回背包（卸下武器时使用）：
        /// 与 AddItem(ItemSO, amount, customName) 不同，这里保留槽位上的 CustomName/WeaponData，
        /// 不会因为重新走"按 ItemSO 新建槽位"的路径而丢失强化数据。
        /// </summary>
        public bool InsertSlot(InventorySlot slot)
        {
            if (slot == null || slot.Item == null || slot.Count <= 0) return false;
            if (!CanInsertSlot()) return false;

            slots.Add(slot);
            NotifyInventoryChanged();
            return true;
        }

        /// <summary>
        /// 修改槽位的自定义名称（打造后改名）：
        /// 只改名字，不影响数量/强化数据，不要求槽位当前必须在 slots 列表中
        /// （装备中的武器槽位同样可以改名）。
        /// </summary>
        public void RenameSlot(InventorySlot slot, string newName)
        {
            if (slot == null) return;
            slot.CustomName = string.IsNullOrWhiteSpace(newName) ? string.Empty : newName.Trim();
            NotifyInventoryChanged();
        }

        /// <summary>
        /// 按分类排序（同分类内回退到按 ID 排序）。
        /// </summary>
        public void SortByCategory()
        {
            SortInventory((a, b) =>
            {
                ItemSO itemA = a != null ? a.Item : null;
                ItemSO itemB = b != null ? b.Item : null;
                int catA = itemA != null ? (int)itemA.Category : int.MaxValue;
                int catB = itemB != null ? (int)itemB.Category : int.MaxValue;
                int cmp = catA.CompareTo(catB);
                if (cmp != 0) return cmp;
                string idA = itemA != null ? itemA.ID : string.Empty;
                string idB = itemB != null ? itemB.ID : string.Empty;
                return string.Compare(idA, idB, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// 按 ID 排序。
        /// </summary>
        public void SortByID()
        {
            SortInventory((a, b) =>
            {
                string idA = a != null && a.Item != null ? a.Item.ID : string.Empty;
                string idB = b != null && b.Item != null ? b.Item.ID : string.Empty;
                return string.Compare(idA, idB, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// 通用排序入口：
        /// 接收一个 Comparison 委托对 slots 排序，
        /// 排序完成后统一广播 NotifyInventoryChanged。
        /// </summary>
        private void SortInventory(Comparison<InventorySlot> comparator)
        {
            if (comparator == null) return;
            slots.Sort(comparator);
            NotifyInventoryChanged();
        }

        /// <summary>
        /// 统一背包变更通知出口：
        /// 仅通过 EventBus 广播，旧的静态委托已删除（订阅方一律走 OnInventoryChanged）。
        /// </summary>
        private void NotifyInventoryChanged()
        {
            EventBus.Raise(new OnInventoryChanged
            {
                Slots = slots
            });
        }
    }
}
