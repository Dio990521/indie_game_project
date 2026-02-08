using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Core.SaveSystem;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Gameplay.Crafting
{
    /// <summary>
    /// 打造系统核心单例：
    /// 架构定位：
    /// - 只负责“数据与规则”，不直接持有任何 UI 逻辑。
    /// - 提供高频查询接口（CanCraft）与原子执行接口（ExecuteCraft）。
    /// - 通过 EventBus 广播结果（OnBlueprintConsumed），与 UI/其他系统解耦。
    ///
    /// 性能策略：
    /// - BlueprintDatabaseSO 在初始化时转 Dictionary，后续按 ID O(1) 查询。
    /// - 背包材料统计在逻辑层用 Dictionary 聚合，避免重复遍历与重复计数。
    /// </summary>
    public class CraftingSystem : MonoSingleton<CraftingSystem>, ISaveable
    {
        [Header("Config")]
        [Tooltip("图纸数据库（静态配置来源）")]
        [SerializeField] private BlueprintDatabaseSO blueprintDatabase;

        [Tooltip("制造成功后自动写入的存档槽位（无 SaveManager 时会跳过）")]
        [SerializeField] private int autoSaveSlotIndex = 0;

        // 图纸索引：BlueprintID -> BlueprintSO（静态数据）
        private readonly Dictionary<string, BlueprintSO> _blueprintById = new Dictionary<string, BlueprintSO>(StringComparer.Ordinal);
        // 图纸记录：BlueprintID -> BlueprintRecord（动态进度数据）
        private readonly Dictionary<string, BlueprintRecord> _recordById = new Dictionary<string, BlueprintRecord>(StringComparer.Ordinal);
        // 材料统计缓存：ItemSO -> 当前背包总数量（用于 CanCraft 快速判断）
        private readonly Dictionary<ItemSO, int> _inventoryCounter = new Dictionary<ItemSO, int>();

        // 存档系统缓存（延迟发现，避免硬依赖）
        private SaveManager _saveManager;
        private bool _isRegisteredToSaveManager;
        private bool _isInitialized;

        /// <summary>
        /// SaveManager 识别该模块的唯一 ID。
        /// </summary>
        public string SaveID => "CraftingSystem";

        protected override void Awake()
        {
            base.Awake();
            // 避免重复实例继续初始化，确保只有真正保留的单例执行后续逻辑
            if (Instance != this) return;
            Initialize();
        }

        private void OnEnable()
        {
            // 尝试向 SaveManager 注册（如果当前场景没有 SaveManager，会在后续保存时再次尝试）
            EnsureSaveRegistration(forceSearch: false);
        }

        private void OnDisable()
        {
            // 生命周期结束时注销，防止 SaveManager 保留无效引用
            if (_isRegisteredToSaveManager && _saveManager != null)
            {
                _saveManager.Unregister(this);
            }
            _isRegisteredToSaveManager = false;
        }

        /// <summary>
        /// 对外查询：根据 ID 获取图纸静态配置。
        /// </summary>
        public BlueprintSO GetBlueprint(string blueprintId)
        {
            Initialize();
            if (string.IsNullOrWhiteSpace(blueprintId)) return null;
            _blueprintById.TryGetValue(blueprintId, out BlueprintSO data);
            return data;
        }

        /// <summary>
        /// 对外查询：获取所有“未消耗图纸记录”，用于 UI 左侧列表生成。
        ///
        /// 注意：
        /// - 返回顺序按数据库顺序，保证 UI 稳定且可预期。
        /// - 这里返回的是记录对象引用，调用方只读使用，不应直接篡改结构。
        /// </summary>
        public void GetAvailableBlueprintRecords(List<BlueprintRecord> output)
        {
            if (output == null) return;
            output.Clear();

            Initialize();
            if (blueprintDatabase == null || blueprintDatabase.Blueprints == null) return;

            for (int i = 0; i < blueprintDatabase.Blueprints.Count; i++)
            {
                BlueprintSO data = blueprintDatabase.Blueprints[i];
                if (data == null || string.IsNullOrWhiteSpace(data.ID)) continue;
                if (!_recordById.TryGetValue(data.ID, out BlueprintRecord record)) continue;
                if (record.IsConsumed) continue;
                output.Add(record);
            }
        }

        /// <summary>
        /// 可制造性检查：
        /// 仅做“规则判断”，不改任何状态。
        /// </summary>
        public bool CanCraft(string blueprintId)
        {
            Initialize();

            if (string.IsNullOrWhiteSpace(blueprintId)) return false;
            if (!_blueprintById.TryGetValue(blueprintId, out BlueprintSO blueprint)) return false;
            if (!_recordById.TryGetValue(blueprintId, out BlueprintRecord record)) return false;
            if (record.IsConsumed) return false;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return false;

            RebuildInventoryCounter(inventory);

            IReadOnlyList<BlueprintRequirement> requirements = blueprint.Requirements;
            for (int i = 0; i < requirements.Count; i++)
            {
                BlueprintRequirement requirement = requirements[i];
                if (requirement == null || requirement.Item == null) return false;

                int need = requirement.Amount;
                int have = _inventoryCounter.TryGetValue(requirement.Item, out int cached) ? cached : 0;
                if (have < need) return false;
            }

            return true;
        }

        /// <summary>
        /// 执行制造：
        /// 按需求严格执行顺序：
        /// 1) 扣除材料
        /// 2) 标记图纸 IsConsumed
        /// 3) 发放成品
        /// 4) 自动保存
        /// 5) 发布 OnBlueprintConsumed
        /// </summary>
        public bool ExecuteCraft(string blueprintId)
        {
            Initialize();

            if (!CanCraft(blueprintId)) return false;
            if (!_blueprintById.TryGetValue(blueprintId, out BlueprintSO blueprint)) return false;
            if (!_recordById.TryGetValue(blueprintId, out BlueprintRecord record)) return false;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return false;

            // 1) 扣除材料
            IReadOnlyList<BlueprintRequirement> requirements = blueprint.Requirements;
            for (int i = 0; i < requirements.Count; i++)
            {
                BlueprintRequirement requirement = requirements[i];
                if (requirement == null || requirement.Item == null) return false;

                bool removed = inventory.RemoveItem(requirement.Item, requirement.Amount);
                if (!removed)
                {
                    // 正常情况下不会进入该分支（因为前面 CanCraft 已校验），
                    // 这里保留防御式判断，避免未来并发/外部篡改导致状态污染。
                    Debug.LogWarning($"[CraftingSystem] RemoveItem failed unexpectedly. BlueprintID={blueprintId}");
                    return false;
                }
            }

            // 2) 标记图纸消耗
            record.IsConsumed = true;

            // 3) 发放成品（若配置缺失则仅跳过发放，不阻断流程）
            if (blueprint.ProductItem != null)
            {
                inventory.AddItem(blueprint.ProductItem, blueprint.ProductAmount);
            }

            // 4) 自动保存（无 SaveManager 时静默跳过）
            TryAutoSave();

            // 5) 广播“图纸已消耗”事件，UI 层据此移除左侧列表项
            EventBus.Raise(new OnBlueprintConsumed
            {
                BlueprintID = blueprintId
            });

            return true;
        }

        /// <summary>
        /// SaveManager 调用：捕获当前打造系统状态。
        /// </summary>
        public object CaptureState()
        {
            Initialize();

            CraftingSaveState state = new CraftingSaveState();
            foreach (KeyValuePair<string, BlueprintRecord> pair in _recordById)
            {
                BlueprintRecord src = pair.Value;
                if (src == null) continue;

                // 深拷贝，避免把运行时对象引用直接暴露给存档容器
                state.Records.Add(new BlueprintRecord(src.ID)
                {
                    IsConsumed = src.IsConsumed
                });
            }
            return state;
        }

        /// <summary>
        /// SaveManager 调用：恢复打造系统状态。
        /// </summary>
        public void RestoreState(object data)
        {
            Initialize();

            if (!(data is CraftingSaveState state) || state.Records == null)
            {
                // 数据异常时回到“数据库默认记录”策略，保证系统可继续运行
                EnsureRecordMapConsistency();
                return;
            }

            _recordById.Clear();
            for (int i = 0; i < state.Records.Count; i++)
            {
                BlueprintRecord record = state.Records[i];
                if (record == null || string.IsNullOrWhiteSpace(record.ID)) continue;

                _recordById[record.ID] = new BlueprintRecord(record.ID)
                {
                    IsConsumed = record.IsConsumed
                };
            }

            // 补全数据库新增图纸、清理无效旧 ID
            EnsureRecordMapConsistency();
        }

        /// <summary>
        /// 初始化入口：
        /// - 建立图纸 Dictionary 索引
        /// - 对齐记录数据完整性
        /// </summary>
        private void Initialize()
        {
            if (_isInitialized) return;
            RebuildBlueprintIndex();
            EnsureRecordMapConsistency();
            _isInitialized = true;
        }

        /// <summary>
        /// 从数据库构建图纸索引（List -> Dictionary）。
        /// </summary>
        private void RebuildBlueprintIndex()
        {
            _blueprintById.Clear();

            if (blueprintDatabase == null || blueprintDatabase.Blueprints == null)
            {
                Debug.LogWarning("[CraftingSystem] Missing BlueprintDatabaseSO.");
                return;
            }

            for (int i = 0; i < blueprintDatabase.Blueprints.Count; i++)
            {
                BlueprintSO data = blueprintDatabase.Blueprints[i];
                if (data == null) continue;
                if (string.IsNullOrWhiteSpace(data.ID))
                {
                    Debug.LogWarning("[CraftingSystem] Blueprint has empty ID, ignored.");
                    continue;
                }

                if (_blueprintById.ContainsKey(data.ID))
                {
                    Debug.LogWarning($"[CraftingSystem] Duplicate Blueprint ID ignored: {data.ID}");
                    continue;
                }

                _blueprintById.Add(data.ID, data);
            }
        }

        /// <summary>
        /// 保证图纸记录与数据库一致：
        /// - 数据库有但记录缺失：补默认记录
        /// - 记录存在但数据库无该 ID：移除无效记录
        /// </summary>
        private void EnsureRecordMapConsistency()
        {
            // 1) 清理“数据库已不存在”的记录
            List<string> staleIds = null;
            foreach (KeyValuePair<string, BlueprintRecord> pair in _recordById)
            {
                if (_blueprintById.ContainsKey(pair.Key)) continue;
                if (staleIds == null) staleIds = new List<string>();
                staleIds.Add(pair.Key);
            }

            if (staleIds != null)
            {
                for (int i = 0; i < staleIds.Count; i++)
                {
                    _recordById.Remove(staleIds[i]);
                }
            }

            // 2) 补齐“数据库存在但记录不存在”的条目
            foreach (KeyValuePair<string, BlueprintSO> pair in _blueprintById)
            {
                string id = pair.Key;

                if (!_recordById.ContainsKey(id))
                {
                    _recordById[id] = new BlueprintRecord(id);
                }
            }
        }

        /// <summary>
        /// 统计当前背包中每种材料的总数量。
        /// </summary>
        private void RebuildInventoryCounter(InventoryManager inventory)
        {
            _inventoryCounter.Clear();
            if (inventory == null || inventory.slots == null) return;

            for (int i = 0; i < inventory.slots.Count; i++)
            {
                InventorySlot slot = inventory.slots[i];
                if (slot == null || slot.Item == null || slot.Count <= 0) continue;

                if (_inventoryCounter.TryGetValue(slot.Item, out int current))
                {
                    _inventoryCounter[slot.Item] = current + slot.Count;
                }
                else
                {
                    _inventoryCounter[slot.Item] = slot.Count;
                }
            }
        }

        /// <summary>
        /// 尝试自动保存：
        /// 没有 SaveManager 时静默返回，不影响制造主流程。
        /// </summary>
        private void TryAutoSave()
        {
            EnsureSaveRegistration(forceSearch: true);
            if (_saveManager == null) return;
            _ = _saveManager.SaveAsync(autoSaveSlotIndex);
        }

        /// <summary>
        /// 确保已向 SaveManager 注册当前模块。
        /// </summary>
        private void EnsureSaveRegistration(bool forceSearch)
        {
            if (_isRegisteredToSaveManager) return;

            _saveManager = ResolveSaveManager(forceSearch);
            if (_saveManager == null) return;

            _saveManager.Register(this);
            _isRegisteredToSaveManager = true;
        }

        /// <summary>
        /// 查找 SaveManager：
        /// 使用场景查找避免强依赖，且不会触发 MonoSingleton.Instance 的警告日志。
        /// </summary>
        private SaveManager ResolveSaveManager(bool forceSearch)
        {
            if (_saveManager != null) return _saveManager;
            if (!forceSearch && _isRegisteredToSaveManager) return null;
            return FindAnyObjectByType<SaveManager>();
        }

        /// <summary>
        /// 打造系统存档结构：
        /// 仅保存“图纸记录动态状态”。
        /// </summary>
        [Serializable]
        private class CraftingSaveState
        {
            public List<BlueprintRecord> Records = new List<BlueprintRecord>();
        }
    }
}
