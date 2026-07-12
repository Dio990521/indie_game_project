using System;
using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;

namespace IndieGame.Gameplay.Memory
{
    /// <summary>
    /// Memory 系统（数据层单例）：
    /// 追踪玩家游玩过程中的积累记录，为图鉴 UI 提供数据源。
    ///
    /// 五类数据的来源策略：
    /// 1) 图纸  - 监听 BlueprintObtainedEvent，Set 去重存 ID
    /// 2) 武器  - 监听 CraftHistoryRecordedEvent，追加到有序列表
    /// 3) 道具  - 监听 OnInventoryChanged，记录 Consumable/Equipment/Quest 类型的 ItemSO.ID
    /// 4) 素材  - 监听 OnInventoryChanged，记录 Material 类型的 ItemSO.ID
    /// 5) 任务  - 预留，暂不填充
    /// </summary>
    public class MemorySystem : SaveableMonoSingleton<MemorySystem>
    {
        public override string SaveID => "MemorySystem";

        // ── 内部数据 ──────────────────────────────────────────────────────

        // 1) 至今获得过的所有图纸 ID（含已消耗）
        private readonly HashSet<string> _obtainedBlueprintIds = new HashSet<string>(StringComparer.Ordinal);

        // 2) 至今打造过的武器（蓝图ID + 定制名），保持添加顺序
        private readonly List<CraftedWeaponEntry> _craftedWeapons = new List<CraftedWeaponEntry>();
        // 去重键：BlueprintID|CustomName
        private readonly HashSet<string> _craftedDedupeKeys = new HashSet<string>(StringComparer.Ordinal);

        // 3/4) 至今见过的所有物品 ID（道具与素材共用一个集合，UI 层按 Category 过滤）
        private readonly HashSet<string> _seenItemIds = new HashSet<string>(StringComparer.Ordinal);

        // 5) 完成的任务 ID（预留）
        private readonly List<string> _completedTaskIds = new List<string>();

        // ── 只读对外接口 ──────────────────────────────────────────────────

        public IReadOnlyCollection<string> ObtainedBlueprintIds => _obtainedBlueprintIds;
        public IReadOnlyList<CraftedWeaponEntry> CraftedWeapons => _craftedWeapons;
        public IReadOnlyCollection<string> SeenItemIds => _seenItemIds;
        public IReadOnlyList<string> CompletedTaskIds => _completedTaskIds;

        // ── 生命周期 ──────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable(); // SaveableMonoSingleton 注册到 SaveManager

            EventBus.Subscribe<BlueprintObtainedEvent>(HandleBlueprintObtained);
            EventBus.Subscribe<CraftHistoryRecordedEvent>(HandleCraftHistoryRecorded);
            EventBus.Subscribe<OnInventoryChanged>(HandleInventoryChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable(); // SaveableMonoSingleton 从 SaveManager 注销

            EventBus.Unsubscribe<BlueprintObtainedEvent>(HandleBlueprintObtained);
            EventBus.Unsubscribe<CraftHistoryRecordedEvent>(HandleCraftHistoryRecorded);
            EventBus.Unsubscribe<OnInventoryChanged>(HandleInventoryChanged);
        }

        // ── 事件处理器 ────────────────────────────────────────────────────

        private void HandleBlueprintObtained(BlueprintObtainedEvent evt)
        {
            if (string.IsNullOrWhiteSpace(evt.BlueprintID)) return;
            _obtainedBlueprintIds.Add(evt.BlueprintID);
        }

        private void HandleCraftHistoryRecorded(CraftHistoryRecordedEvent evt)
        {
            if (string.IsNullOrWhiteSpace(evt.BlueprintID)) return;
            // 同一图纸可打造同名武器，但 BlueprintID+CustomName 完全相同才去重
            string dedupeKey = evt.BlueprintID + "|" + (evt.CustomName ?? string.Empty).Trim();
            if (_craftedDedupeKeys.Add(dedupeKey))
                _craftedWeapons.Add(new CraftedWeaponEntry(evt.BlueprintID, evt.CustomName ?? string.Empty));
        }

        private void HandleInventoryChanged(OnInventoryChanged evt)
        {
            if (evt.Slots == null) return;
            for (int i = 0; i < evt.Slots.Count; i++)
            {
                var slot = evt.Slots[i];
                if (slot?.Item == null) continue;
                _seenItemIds.Add(slot.Item.ID);
            }
        }

        // ── ISaveable 存档接口 ────────────────────────────────────────────

        public override object CaptureState()
        {
            var state = new MemorySaveState();

            foreach (string id in _obtainedBlueprintIds)
                state.ObtainedBlueprintIds.Add(id);

            foreach (var entry in _craftedWeapons)
                state.CraftedWeapons.Add(new CraftedWeaponRecord
                    { BlueprintID = entry.BlueprintID, CustomName = entry.CustomName });

            foreach (string id in _seenItemIds)
                state.SeenItemIds.Add(id);

            foreach (string id in _completedTaskIds)
                state.CompletedTaskIds.Add(id);

            return state;
        }

        public override void RestoreState(object data)
        {
            if (!(data is MemorySaveState state)) return;

            _obtainedBlueprintIds.Clear();
            if (state.ObtainedBlueprintIds != null)
                foreach (string id in state.ObtainedBlueprintIds)
                    if (!string.IsNullOrWhiteSpace(id)) _obtainedBlueprintIds.Add(id);

            _craftedWeapons.Clear();
            _craftedDedupeKeys.Clear();
            if (state.CraftedWeapons != null)
            {
                foreach (var rec in state.CraftedWeapons)
                {
                    if (rec == null || string.IsNullOrWhiteSpace(rec.BlueprintID)) continue;
                    string key = rec.BlueprintID + "|" + (rec.CustomName ?? string.Empty).Trim();
                    if (_craftedDedupeKeys.Add(key))
                        _craftedWeapons.Add(new CraftedWeaponEntry(rec.BlueprintID, rec.CustomName ?? string.Empty));
                }
            }

            _seenItemIds.Clear();
            if (state.SeenItemIds != null)
                foreach (string id in state.SeenItemIds)
                    if (!string.IsNullOrWhiteSpace(id)) _seenItemIds.Add(id);

            _completedTaskIds.Clear();
            if (state.CompletedTaskIds != null)
                foreach (string id in state.CompletedTaskIds)
                    if (!string.IsNullOrWhiteSpace(id)) _completedTaskIds.Add(id);
        }

        // ── 内部数据类型 ──────────────────────────────────────────────────

        /// <summary>武器制作记录（运行时用，含确定性访问）。</summary>
        public readonly struct CraftedWeaponEntry
        {
            public readonly string BlueprintID;
            public readonly string CustomName;
            public CraftedWeaponEntry(string blueprintID, string customName)
            {
                BlueprintID = blueprintID;
                CustomName = customName;
            }
        }

        /// <summary>存档序列化数据根节点。</summary>
        [Serializable]
        public class MemorySaveState
        {
            public List<string> ObtainedBlueprintIds = new List<string>();
            public List<CraftedWeaponRecord> CraftedWeapons = new List<CraftedWeaponRecord>();
            public List<string> SeenItemIds = new List<string>();
            public List<string> CompletedTaskIds = new List<string>();
        }

        /// <summary>武器制作记录（序列化用 class，不可为 readonly struct）。</summary>
        [Serializable]
        public class CraftedWeaponRecord
        {
            public string BlueprintID;
            public string CustomName;
        }
    }
}
