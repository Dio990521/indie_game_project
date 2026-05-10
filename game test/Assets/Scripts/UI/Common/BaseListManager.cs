using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.UI.Common
{
    /// <summary>
    /// 列表管理器泛型基类（纯 C# 辅助类，无 MonoBehaviour 生命周期）：
    /// 把 CraftingListManager / ShopListManager 中重复出现的“对象池 + 条目索引 + 选中状态”
    /// 这套模板抽离到这里，子类只需要负责自己的 Rebuild / AddEntry 业务。
    ///
    /// 类型参数：
    /// - TEntry：条目数据结构（一般是 struct，由子类定义）。
    /// - TSlot：Slot UI 组件类型（必须继承自 Component，便于通过 .gameObject 进入对象池回收）。
    ///
    /// 设计取舍：
    /// - 不内置 AddEntry，因为各子类条目结构不同（ShopListEntry 自带 Icon，
    ///   CraftListEntry 需要外部传入 Icon），强行抽出反而需要更复杂的回调；
    /// - 提供 RegisterActiveSlot / ReleaseSlotByKey 等模板方法，让子类把通用的“激活/回收”逻辑复用。
    /// </summary>
    public abstract class BaseListManager<TEntry, TSlot> where TSlot : Component
    {
        // 对象池：构建 Slot 时统一通过它获取/回收
        protected GameObjectPool _slotPool;
        // 当前所有激活的 Slot UI 组件
        protected readonly List<TSlot> _activeSlots = new List<TSlot>();
        // 条目数据索引：EntryKey -> TEntry
        protected readonly Dictionary<string, TEntry> _entryByKey = new Dictionary<string, TEntry>(StringComparer.Ordinal);
        // 条目顺序：保持稳定的展示顺序
        protected readonly List<string> _entryOrder = new List<string>();

        /// <summary> 当前选中条目的 key（无选中时为 null/空字符串）。</summary>
        public string SelectedEntryKey { get; protected set; }

        /// <summary> 当前条目顺序的只读视图。</summary>
        public IReadOnlyList<string> EntryOrder => _entryOrder;

        /// <summary>
        /// 回收所有列表项并清空索引。子类如需追加清理动作可 override 并调用 base.ReleaseAll()。
        /// </summary>
        public virtual void ReleaseAll()
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                if (_activeSlots[i] != null && _slotPool != null)
                    _slotPool.Release(_activeSlots[i].gameObject);
            }
            _activeSlots.Clear();
            _entryByKey.Clear();
            _entryOrder.Clear();
            SelectedEntryKey = null;
        }

        /// <summary>
        /// 尝试获取指定 key 的条目数据。
        /// </summary>
        public bool TryGetEntry(string key, out TEntry entry)
        {
            return _entryByKey.TryGetValue(key, out entry);
        }

        /// <summary>
        /// 记录当前选中条目的 key（key 不在索引中则忽略）。
        /// </summary>
        public virtual void Select(string entryKey)
        {
            if (_entryByKey.ContainsKey(entryKey))
                SelectedEntryKey = entryKey;
        }

        /// <summary>
        /// 模板方法：把一个新建/池化的 Slot 登记进 _activeSlots / _entryByKey / _entryOrder。
        /// 子类通常在 AddEntry 内部调用本方法，避免重复维护三个集合的一致性。
        /// </summary>
        protected void RegisterActiveSlot(string entryKey, TEntry entry, TSlot slot)
        {
            if (string.IsNullOrEmpty(entryKey) || slot == null) return;
            _activeSlots.Add(slot);
            _entryByKey[entryKey] = entry;
            _entryOrder.Add(entryKey);
        }
    }
}
