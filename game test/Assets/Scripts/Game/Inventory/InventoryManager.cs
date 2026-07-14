using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Gameplay.Equipment;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 背包管理器：作为单例存在，管理玩家的所有物品。
    /// 负责物品的存储、背包界面的开启与关闭，以及物品的使用逻辑分发。
    /// 继承自 SaveableMonoSingleton：
    /// - 背包槽位（含 CustomName）与"当前装备中的武器/防具槽位"一并参与存档；
    /// - 读档时通过 ItemDatabaseSO 按 ItemSO.ID 反查资源恢复槽位。
    /// </summary>
    public class InventoryManager : SaveableMonoSingleton<InventoryManager>
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

        [Header("存档")]
        [Tooltip("物品总数据库：读档时按 ItemSO.ID 反查资源。未配置时背包无法从存档恢复。")]
        [SerializeField] private ItemDatabaseSO itemDatabase;

        // 通信方式说明：
        // - 内容变化  → EventBus.Raise(new OnInventoryChanged { Slots = slots });
        // - 已打开    → EventBus.Raise(new InventoryOpenedEvent());
        // - 已关闭    → EventBus.Raise(new InventoryClosedEvent());
        // 旧的静态委托（OnInventoryUpdated/OnInventoryOpened/OnInventoryClosed）已移除，
        // 历史订阅方应改为 EventBus.Subscribe<...> 形式（详见 P1-3 迁移说明）。

        // 初始化标记
        private bool _isInitialized;

        // ── 存档相关 ─────────────────────────────────────────────────────
        // 物品索引：ItemSO.ID -> ItemSO（读档反查用，懒构建）
        private Dictionary<string, ItemSO> _itemById;
        // 读档时缓存的"装备恢复数据"：玩家对象可能晚于读档创建，需延迟应用
        private InventorySlotSaveData _pendingWeaponRestore;
        private InventorySlotSaveData _pendingArmorRestore;
        private bool _hasPendingEquipmentRestore;
        private Coroutine _equipmentRestoreRoutine;

        // 槽位存放位置标记（存档用）
        private const int LocationBag = 0;
        private const int LocationWeapon = 1;
        private const int LocationArmor = 2;

        /// <summary> 存档模块唯一标识。 </summary>
        public override string SaveID => "InventoryManager";

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            // Awake 里强制尝试一次注册，保证背包在最早阶段就可参与存档
            // （与 GoldSystem / ShopSystem 保持同一模式）。
            EnsureSaveRegistration(forceSearch: true);
        }

        protected override void OnEnable()
        {
            // 父类完成 SaveManager 注册（幂等）
            base.OnEnable();
            // 通过事件总线订阅“打开背包”事件。
            // 这种解耦方式允许任何地方（如棋盘菜单或快捷键）发出 OpenInventoryEvent 即可打开背包。
            EventBus.Subscribe<OpenInventoryEvent>(HandleOpenInventory);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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
        /// 添加道具（事务性）：
        /// - 先用 CanAddItem 做整体可行性预检，空间不足时**不做任何修改**直接返回 false；
        /// - 预检通过后再执行“堆叠已有槽位 → 新建槽位”，保证要么全部加入、要么完全不动。
        ///
        /// 修复说明（C2）：
        /// 旧实现会先堆叠再新建，容量不足时中途 return false，导致“失败但已部分入包”，
        /// 上游（商店退款回滚 / 打造发放）以为完全失败，玩家会白拿部分物品或丢失成品。
        /// </summary>
        /// <param name="customName">
        /// 物品实例自定义名称：
        /// - null/空：使用道具原始名称（可与同类默认名称堆叠）
        /// - 非空：作为实例名写入槽位，仅与“同名同类”槽位堆叠
        /// </param>
        public bool AddItem(ItemSO item, int amount = 1, string customName = null)
        {
            if (item == null || amount <= 0) return false;

            // 事务性预检：放不下就整体失败，不产生任何部分写入
            if (!CanAddItem(item, amount, customName)) return false;

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

            // 2) 新建槽位（预检已保证容量足够，这里的容量判断仅为防御式兜底）
            while (remaining > 0)
            {
                if (slots.Count >= maxCapacity)
                {
                    DebugTools.LogError("[InventoryManager] AddItem 预检通过但容量不足，请检查 CanAddItem 与 AddItem 的规则是否一致。");
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
        /// 移除道具（按 ItemSO 弱匹配）：
        /// 按数量扣减，扣完后清理空槽位。
        /// 注意：本方法忽略 CustomName，只按 ItemSO 匹配，且从列表末尾开始扣。
        /// 若要精确删除“某一个槽位”（如玩家选中的改名武器），请使用
        /// <see cref="RemoveFromSlot"/> 或 <see cref="RemoveSlot"/>。
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
        /// 从指定槽位精确扣减数量（H2 修复新增）：
        /// 与 RemoveItem(ItemSO, amount) 的弱匹配不同，本方法只作用于传入的槽位对象本身，
        /// 不会误扣背包里另一个"同 ItemSO 但不同 CustomName"的槽位（如玩家改名的打造武器）。
        /// 扣到 0 时自动移除该槽位。
        /// </summary>
        /// <returns>true=扣减成功；false=槽位无效或不在背包中</returns>
        public bool RemoveFromSlot(InventorySlot slot, int amount)
        {
            if (slot == null || amount <= 0) return false;
            if (!slots.Contains(slot)) return false;

            if (slot.Count > amount)
            {
                slot.Count -= amount;
            }
            else
            {
                slots.Remove(slot);
            }

            NotifyInventoryChanged();
            return true;
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
        /// 与 AddItem(ItemSO, amount, customName) 不同，这里保留槽位上的 CustomName，
        /// 不会因为重新走"按 ItemSO 新建槽位"的路径而丢失玩家自定义命名。
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

        // ══════════════════════════ 存档接入（C1 修复） ══════════════════════════

        /// <summary>
        /// SaveManager 调用：捕获背包 + 已装备武器/防具的完整物品状态。
        /// 装备中的槽位不在 slots 列表里（装备时被 RemoveSlot 摘出），
        /// 必须单独采集，否则读档后装备会凭空消失。
        /// </summary>
        public override object CaptureState()
        {
            InventorySaveState state = new InventorySaveState();

            // 1) 背包槽位
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotSaveData data = BuildSlotSaveData(slots[i], LocationBag);
                if (data != null) state.Slots.Add(data);
            }

            // 2) 已装备的武器/防具（从玩家对象上的控制器采集）
            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            if (player != null)
            {
                WeaponEquipController weaponCtrl = player.GetComponent<WeaponEquipController>();
                InventorySlotSaveData weaponData = BuildSlotSaveData(weaponCtrl != null ? weaponCtrl.CurrentWeaponSlot : null, LocationWeapon);
                if (weaponData != null) state.Slots.Add(weaponData);

                ArmorEquipController armorCtrl = player.GetComponent<ArmorEquipController>();
                InventorySlotSaveData armorData = BuildSlotSaveData(armorCtrl != null ? armorCtrl.CurrentArmorSlot : null, LocationArmor);
                if (armorData != null) state.Slots.Add(armorData);
            }

            return state;
        }

        /// <summary>
        /// SaveManager 调用：恢复背包与装备状态。
        /// 玩家对象可能晚于读档创建（标题界面读档 → 玩法场景才生成玩家），
        /// 因此装备部分先缓存为 pending，由协程轮询等玩家就绪后再应用。
        /// </summary>
        public override void RestoreState(object data)
        {
            if (!(data is InventorySaveState state) || state.Slots == null) return;

            if (!EnsureItemIndex())
            {
                DebugTools.LogError("[InventoryManager] 未配置 ItemDatabaseSO，无法从存档恢复背包。请在 Inspector 中指定物品数据库。");
                return;
            }

            slots.Clear();
            _pendingWeaponRestore = null;
            _pendingArmorRestore = null;

            for (int i = 0; i < state.Slots.Count; i++)
            {
                InventorySlotSaveData saved = state.Slots[i];
                if (saved == null || string.IsNullOrWhiteSpace(saved.ItemID)) continue;

                switch (saved.Location)
                {
                    case LocationWeapon:
                        _pendingWeaponRestore = saved;
                        break;
                    case LocationArmor:
                        _pendingArmorRestore = saved;
                        break;
                    default:
                        InventorySlot slot = BuildSlotFromSaveData(saved);
                        if (slot != null) slots.Add(slot);
                        break;
                }
            }

            NotifyInventoryChanged();

            // 装备恢复统一走 pending 流程：
            // 即使存档里没有装备条目，也要执行一次"清空当前装备"，
            // 避免"本局装备了武器 → 读了一份没装备的旧档"后武器残留。
            _hasPendingEquipmentRestore = true;
            TryApplyPendingEquipmentRestore();

            if (_hasPendingEquipmentRestore && isActiveAndEnabled)
            {
                // 玩家未就绪：启动轮询协程等待玩家创建后补应用
                if (_equipmentRestoreRoutine != null) StopCoroutine(_equipmentRestoreRoutine);
                _equipmentRestoreRoutine = StartCoroutine(WaitAndApplyEquipmentRestore());
            }
        }

        /// <summary>
        /// 轮询等待玩家对象就绪后应用装备恢复（帧数上限防止无玩家场景下无限等待）。
        /// </summary>
        private IEnumerator WaitAndApplyEquipmentRestore()
        {
            const int maxWaitFrames = 1800; // 约 30 秒（60fps），超时放弃并保留 pending 供下次读档覆盖
            for (int i = 0; i < maxWaitFrames && _hasPendingEquipmentRestore; i++)
            {
                TryApplyPendingEquipmentRestore();
                if (!_hasPendingEquipmentRestore) yield break;
                yield return null;
            }
            _equipmentRestoreRoutine = null;
        }

        /// <summary>
        /// 尝试把 pending 装备数据应用到玩家的装备控制器上。
        /// </summary>
        private void TryApplyPendingEquipmentRestore()
        {
            if (!_hasPendingEquipmentRestore) return;

            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            if (player == null) return;

            WeaponEquipController weaponCtrl = player.GetComponent<WeaponEquipController>();
            if (weaponCtrl != null)
            {
                weaponCtrl.RestoreEquipped(BuildSlotFromSaveData(_pendingWeaponRestore));
            }

            ArmorEquipController armorCtrl = player.GetComponent<ArmorEquipController>();
            if (armorCtrl != null)
            {
                armorCtrl.RestoreEquipped(BuildSlotFromSaveData(_pendingArmorRestore));
            }

            _pendingWeaponRestore = null;
            _pendingArmorRestore = null;
            _hasPendingEquipmentRestore = false;
        }

        /// <summary>
        /// 槽位 → 存档数据（null 槽位返回 null，调用方自行跳过）。
        /// </summary>
        private static InventorySlotSaveData BuildSlotSaveData(InventorySlot slot, int location)
        {
            if (slot == null || slot.Item == null || slot.Count <= 0) return null;
            if (string.IsNullOrWhiteSpace(slot.Item.ID))
            {
                DebugTools.LogWarning($"[InventoryManager] 物品 {slot.Item.name} 缺少 ID，无法写入存档，已跳过。");
                return null;
            }

            return new InventorySlotSaveData
            {
                ItemID = slot.Item.ID,
                Count = slot.Count,
                CustomName = slot.CustomName ?? string.Empty,
                Location = location
            };
        }

        /// <summary>
        /// 存档数据 → 槽位（数据库查不到 ID 时返回 null 并打警告）。
        /// </summary>
        private InventorySlot BuildSlotFromSaveData(InventorySlotSaveData saved)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.ItemID)) return null;
            if (!EnsureItemIndex()) return null;

            if (!_itemById.TryGetValue(saved.ItemID, out ItemSO item) || item == null)
            {
                DebugTools.LogWarning($"[InventoryManager] 存档物品 ID \"{saved.ItemID}\" 在 ItemDatabase 中不存在，已跳过。");
                return null;
            }

            return new InventorySlot(item, Mathf.Max(1, saved.Count), saved.CustomName);
        }

        /// <summary>
        /// 懒构建物品索引（ItemSO.ID -> ItemSO）。
        /// </summary>
        private bool EnsureItemIndex()
        {
            if (_itemById != null) return true;
            if (itemDatabase == null) return false;

            _itemById = DatabaseIndexer.BuildById(
                itemDatabase.Items,
                item => item != null ? item.ID : null,
                "InventoryManager");
            return true;
        }

        /// <summary>
        /// 背包存档结构：
        /// 单列表 + Location 标记（0=背包 / 1=装备武器 / 2=装备防具），
        /// 避免 JsonUtility 对 null 类字段序列化行为不可控的问题。
        /// </summary>
        [Serializable]
        private class InventorySaveState
        {
            public List<InventorySlotSaveData> Slots = new List<InventorySlotSaveData>();
        }

        /// <summary>
        /// 单个槽位的存档结构。
        /// </summary>
        [Serializable]
        private class InventorySlotSaveData
        {
            public string ItemID;
            public int Count;
            public string CustomName;
            public int Location;
        }
    }
}
