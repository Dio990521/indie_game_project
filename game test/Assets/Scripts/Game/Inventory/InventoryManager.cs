using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.UI;
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
        // 覆盖单例属性：在加载新场景时销毁。
        // 这通常意味着背包数据由存档系统另行管理，或者背包仅存在于特定模式。
        protected override bool DestroyOnLoad => true;

        [Header("数据")]
        [Tooltip("背包最大容量（槽位数）。")]
        public int maxCapacity = 30;

        [Tooltip("当前背包槽位列表。每个槽位包含道具与数量。")]
        public List<InventorySlot> slots = new List<InventorySlot>();

        // --- 事件回调 ---

        /// <summary> 当背包内容发生变化（或初次打开同步数据）时触发，传递最新的槽位列表 </summary>
        public static event Action<List<InventorySlot>> OnInventoryUpdated;

        /// <summary> 当背包界面被请求打开时触发 </summary>
        public static event Action OnInventoryOpened;

        /// <summary> 当背包界面被请求关闭时触发 </summary>
        public static event Action OnInventoryClosed;

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
            // 2. 发送打开指令，触发 UI 动画或显示 Canvas
            OnInventoryOpened?.Invoke();
        }

        /// <summary>
        /// 关闭背包：通知 UI 层级隐藏界面。
        /// </summary>
        public void CloseInventory()
        {
            OnInventoryClosed?.Invoke();
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
        /// 按分类排序。
        /// </summary>
        public void SortByCategory()
        {
            slots.Sort((a, b) =>
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
            NotifyInventoryChanged();
        }

        /// <summary>
        /// 按 ID 排序。
        /// </summary>
        public void SortByID()
        {
            slots.Sort((a, b) =>
            {
                string idA = a != null && a.Item != null ? a.Item.ID : string.Empty;
                string idB = b != null && b.Item != null ? b.Item.ID : string.Empty;
                return string.Compare(idA, idB, StringComparison.Ordinal);
            });
            NotifyInventoryChanged();
        }

        /// <summary>
        /// 统一背包变更通知出口：
        /// 同时兼容旧版静态事件（OnInventoryUpdated）与新版 EventBus 事件（OnInventoryChanged）。
        /// </summary>
        private void NotifyInventoryChanged()
        {
            // 兼容已有背包 UI 监听逻辑
            OnInventoryUpdated?.Invoke(slots);
            // 提供给新系统（如打造系统）做解耦监听
            EventBus.Raise(new OnInventoryChanged
            {
                Slots = slots
            });
        }
    }
}
