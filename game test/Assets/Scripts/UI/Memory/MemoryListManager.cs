using IndieGame.Core.Utilities;
using IndieGame.UI.Common;
using UnityEngine;

namespace IndieGame.UI.Memory
{
    /// <summary>
    /// Memory 图鉴列表管理器：
    /// 继承 BaseListManager，负责对象池管理与条目注册。
    /// 所有 Tab 使用同一 MemoryEntry 结构体，通过 MemorySlotUI 统一渲染。
    /// 由 MemoryUIController 持有，每次 Tab 切换时先 ReleaseAll 再逐条 AddEntry。
    /// </summary>
    internal class MemoryListManager : BaseListManager<MemoryListManager.MemoryEntry, MemorySlotUI>
    {
        /// <summary>
        /// 通用条目结构体：适配所有 Tab 的显示信息。
        /// </summary>
        internal struct MemoryEntry
        {
            // ListManager 内部索引键，格式如 "BP:0" / "IT:3" / "WD:1"
            public string EntryKey;
            // 原始业务 ID（BlueprintID / ItemID / WordID 等），用于详情面板反查
            public string PrimaryID;
            public string DisplayName;
            // 副标签（如"持有中"、"装备"、"语料"等）
            public string Subtitle;
            // 详情面板描述文本
            public string Description;
            public Sprite Icon;
        }

        private Transform _listRoot;

        /// <summary>
        /// 初始化对象池与列表根节点。
        /// </summary>
        public void Init(GameObject slotPrefab, Transform listRoot, int warmup)
        {
            _listRoot = listRoot;
            if (slotPrefab != null && listRoot != null)
                _slotPool = new GameObjectPool(slotPrefab, listRoot, warmup);
        }

        /// <summary>
        /// 添加一个条目并从对象池取出对应 Slot。
        /// </summary>
        public void AddEntry(MemoryEntry entry)
        {
            if (_slotPool == null || _listRoot == null) return;

            GameObject go = _slotPool.Get();
            go.transform.SetParent(_listRoot, false);

            MemorySlotUI slotUI = go.GetComponent<MemorySlotUI>();
            if (slotUI == null)
            {
                DebugTools.LogError("[MemoryListManager] slotPrefab 缺少 MemorySlotUI 组件。");
                _slotPool.Release(go);
                return;
            }

            slotUI.Setup(entry.EntryKey, entry.Icon, entry.DisplayName, entry.Subtitle);
            RegisterActiveSlot(entry.EntryKey, entry, slotUI);
        }
    }
}
