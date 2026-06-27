using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Dialogue;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Memory;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Equipment
{
    /// <summary>
    /// 武器强化系统：
    /// 负责"叠加强化前缀（最多5个，不可重复）/重铸前缀"的规则判断与执行，
    /// 以及"基础Modifiers+已应用前缀加成"的合并计算、强化后武器显示名称的拼接。
    /// 只负责数据与规则，不持有任何 UI 逻辑（参照 CraftingSystem 的职责边界）。
    /// </summary>
    public class WeaponEnhanceSystem : MonoSingleton<WeaponEnhanceSystem>
    {
        private const int MaxPrefixCount = 5;

        [Header("Config")]
        [SerializeField] private WeaponEnhanceConfigSO enhanceConfig;
        [SerializeField] private WordDatabaseSO wordDatabase;

        // WordSO.ID -> WordSO 索引，懒构建
        private Dictionary<string, WordSO> _wordById;

        /// <summary>
        /// 按 ID 查询词条（语料库列表/前缀反查均走这里）。
        /// </summary>
        public WordSO GetWord(string wordId)
        {
            EnsureWordIndex();
            if (string.IsNullOrWhiteSpace(wordId)) return null;
            _wordById.TryGetValue(wordId, out WordSO word);
            return word;
        }

        /// <summary>
        /// 语料库中所有"已解锁且可用于武器强化"的词条。
        /// </summary>
        public void GetAvailablePrefixWords(List<WordSO> output)
        {
            if (output == null) return;
            output.Clear();

            EnsureWordIndex();
            IReadOnlyCollection<string> learned = MemorySystem.Instance != null
                ? MemorySystem.Instance.LearnedWordIds
                : null;
            if (learned == null) return;

            foreach (KeyValuePair<string, WordSO> pair in _wordById)
            {
                WordSO word = pair.Value;
                if (word == null || !word.IsWeaponPrefix) continue;
                if (!learned.Contains(word.ID)) continue;
                output.Add(word);
            }
        }

        // ── 强化 ─────────────────────────────────────────────────────────

        public bool CanEnhance(InventorySlot weaponSlot, string wordId, out string failReason)
        {
            failReason = string.Empty;

            WeaponSO weapon = weaponSlot?.Item as WeaponSO;
            if (weapon == null)
            {
                failReason = "选中的不是武器";
                return false;
            }

            WeaponInstanceData data = EnsureWeaponData(weaponSlot);
            if (data.AppliedPrefixWordIds.Count >= MaxPrefixCount)
            {
                failReason = "强化次数已达上限";
                return false;
            }

            if (!IsWordUsablePrefix(wordId, out failReason)) return false;
            if (data.AppliedPrefixWordIds.Contains(wordId))
            {
                failReason = "该词缀已应用在这把武器上";
                return false;
            }

            IReadOnlyList<BlueprintRequirement> cost = GetEnhanceCost(data.AppliedPrefixWordIds.Count);
            if (!HasEnoughMaterials(cost))
            {
                failReason = "材料不足";
                return false;
            }

            return true;
        }

        public bool ExecuteEnhance(InventorySlot weaponSlot, string wordId)
        {
            if (!CanEnhance(weaponSlot, wordId, out _)) return false;

            WeaponInstanceData data = EnsureWeaponData(weaponSlot);
            IReadOnlyList<BlueprintRequirement> cost = GetEnhanceCost(data.AppliedPrefixWordIds.Count);
            if (!ConsumeMaterials(cost)) return false;

            data.AppliedPrefixWordIds.Add(wordId);
            RefreshIfEquipped(weaponSlot);

            EventBus.Raise(new WeaponEnhancedEvent { Slot = weaponSlot, WordId = wordId });
            NotifyInventoryChanged();
            return true;
        }

        // ── 重铸 ─────────────────────────────────────────────────────────

        public bool CanRebind(InventorySlot weaponSlot, int prefixIndex, string newWordId, out string failReason)
        {
            failReason = string.Empty;

            WeaponSO weapon = weaponSlot?.Item as WeaponSO;
            if (weapon == null)
            {
                failReason = "选中的不是武器";
                return false;
            }

            WeaponInstanceData data = EnsureWeaponData(weaponSlot);
            if (prefixIndex < 0 || prefixIndex >= data.AppliedPrefixWordIds.Count)
            {
                failReason = "前缀位序号不合法";
                return false;
            }

            if (!IsWordUsablePrefix(newWordId, out failReason)) return false;
            if (data.AppliedPrefixWordIds.Contains(newWordId))
            {
                failReason = "该词缀已应用在这把武器上";
                return false;
            }

            IReadOnlyList<BlueprintRequirement> cost = GetRebindCost(prefixIndex);
            if (!HasEnoughMaterials(cost))
            {
                failReason = "材料不足";
                return false;
            }

            return true;
        }

        public bool ExecuteRebind(InventorySlot weaponSlot, int prefixIndex, string newWordId)
        {
            if (!CanRebind(weaponSlot, prefixIndex, newWordId, out _)) return false;

            WeaponInstanceData data = EnsureWeaponData(weaponSlot);
            string oldWordId = data.AppliedPrefixWordIds[prefixIndex];

            IReadOnlyList<BlueprintRequirement> cost = GetRebindCost(prefixIndex);
            if (!ConsumeMaterials(cost)) return false;

            data.AppliedPrefixWordIds[prefixIndex] = newWordId;
            RefreshIfEquipped(weaponSlot);

            EventBus.Raise(new WeaponRebindEvent
            {
                Slot = weaponSlot,
                PrefixIndex = prefixIndex,
                OldWordId = oldWordId,
                NewWordId = newWordId
            });
            NotifyInventoryChanged();
            return true;
        }

        // ── 加成合并 / 名称拼接（供 UI 与 WeaponEquipController 共用） ──────

        /// <summary>
        /// 合并"武器基础 Modifiers + 已应用前缀的数值加成"。
        /// </summary>
        public List<StatModifierData> ComposeEffectiveModifiers(WeaponSO weapon, WeaponInstanceData data)
        {
            List<StatModifierData> result = new List<StatModifierData>();
            if (weapon == null) return result;

            result.AddRange(weapon.Modifiers);

            if (data != null)
            {
                EnsureWordIndex();
                for (int i = 0; i < data.AppliedPrefixWordIds.Count; i++)
                {
                    WordSO word = GetWord(data.AppliedPrefixWordIds[i]);
                    if (word == null) continue;
                    result.AddRange(word.PrefixModifiers);
                }
            }
            return result;
        }

        /// <summary>
        /// 异步拼接武器显示名称："前缀1·前缀2·...·(CustomName 或原始名称)"，按应用顺序排列。
        /// 前缀文本来自 WordSO.DisplayName（本地化），全部解析完成后一次性回调，避免文字逐条跳变。
        /// </summary>
        public void ComposeDisplayName(ItemSO weapon, string customName, WeaponInstanceData data, Action<string> onReady)
        {
            List<string> prefixIds = data?.AppliedPrefixWordIds;
            if (prefixIds == null || prefixIds.Count == 0)
            {
                onReady?.Invoke(GetBaseName(weapon, customName));
                return;
            }

            EnsureWordIndex();
            string[] resolvedParts = new string[prefixIds.Count];
            int remaining = prefixIds.Count;

            for (int i = 0; i < prefixIds.Count; i++)
            {
                int index = i; // 闭包捕获，避免循环变量复用问题
                WordSO word = GetWord(prefixIds[index]);
                if (word == null || word.DisplayName == null)
                {
                    resolvedParts[index] = string.Empty;
                    remaining--;
                    if (remaining <= 0) FinishComposeDisplayName(resolvedParts, weapon, customName, onReady);
                    continue;
                }

                var handle = word.DisplayName.GetLocalizedStringAsync();
                handle.Completed += op =>
                {
                    resolvedParts[index] = op.Result;
                    remaining--;
                    if (remaining <= 0) FinishComposeDisplayName(resolvedParts, weapon, customName, onReady);
                };
            }
        }

        private static void FinishComposeDisplayName(string[] prefixParts, ItemSO weapon, string customName, Action<string> onReady)
        {
            List<string> parts = new List<string>(prefixParts.Length + 1);
            for (int i = 0; i < prefixParts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(prefixParts[i])) parts.Add(prefixParts[i]);
            }
            parts.Add(GetBaseName(weapon, customName));
            onReady?.Invoke(string.Join("·", parts));
        }

        private static string GetBaseName(ItemSO weapon, string customName)
        {
            if (!string.IsNullOrWhiteSpace(customName)) return customName;
            return weapon != null ? weapon.GetLocalizedName() : "Unknown";
        }

        // ── 内部工具 ─────────────────────────────────────────────────────

        private bool IsWordUsablePrefix(string wordId, out string failReason)
        {
            failReason = string.Empty;

            WordSO word = GetWord(wordId);
            if (word == null || !word.IsWeaponPrefix)
            {
                failReason = "该词语不能用于强化";
                return false;
            }

            if (MemorySystem.Instance == null || !MemorySystem.Instance.LearnedWordIds.Contains(wordId))
            {
                failReason = "尚未解锁该词语";
                return false;
            }

            return true;
        }

        private static WeaponInstanceData EnsureWeaponData(InventorySlot weaponSlot)
        {
            if (weaponSlot.WeaponData == null) weaponSlot.WeaponData = new WeaponInstanceData();
            return weaponSlot.WeaponData;
        }

        /// <summary>
        /// 第 stepIndex 次强化所需材料（供 UI 显示消耗预览，也供内部校验/扣除复用）。
        /// </summary>
        public IReadOnlyList<BlueprintRequirement> GetEnhanceCost(int stepIndex)
        {
            return enhanceConfig != null ? enhanceConfig.GetEnhanceCost(stepIndex) : Array.Empty<BlueprintRequirement>();
        }

        /// <summary>
        /// 替换第 stepIndex 个前缀位所需材料（供 UI 显示消耗预览，也供内部校验/扣除复用）。
        /// </summary>
        public IReadOnlyList<BlueprintRequirement> GetRebindCost(int stepIndex)
        {
            return enhanceConfig != null ? enhanceConfig.GetRebindCost(stepIndex) : Array.Empty<BlueprintRequirement>();
        }

        private static bool HasEnoughMaterials(IReadOnlyList<BlueprintRequirement> cost)
        {
            if (cost == null || cost.Count == 0) return true;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return false;

            for (int i = 0; i < cost.Count; i++)
            {
                BlueprintRequirement requirement = cost[i];
                if (requirement?.Item == null) continue;
                if (!HasItemAmount(inventory, requirement.Item, requirement.Amount)) return false;
            }
            return true;
        }

        private static bool HasItemAmount(InventoryManager inventory, ItemSO item, int amount)
        {
            int total = 0;
            for (int i = 0; i < inventory.slots.Count; i++)
            {
                InventorySlot slot = inventory.slots[i];
                if (slot?.Item != item) continue;
                total += slot.Count;
                if (total >= amount) return true;
            }
            return total >= amount;
        }

        private static bool ConsumeMaterials(IReadOnlyList<BlueprintRequirement> cost)
        {
            if (cost == null || cost.Count == 0) return true;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return false;

            for (int i = 0; i < cost.Count; i++)
            {
                BlueprintRequirement requirement = cost[i];
                if (requirement?.Item == null) continue;
                // CanEnhance/CanRebind 已校验过材料充足，这里正常不会失败；保留防御式判断避免状态污染
                if (!inventory.RemoveItem(requirement.Item, requirement.Amount))
                {
                    DebugTools.LogWarning("[WeaponEnhanceSystem] RemoveItem failed unexpectedly during enhance/rebind.");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 若强化/重铸的正是当前装备的武器，让加成立即在 CharacterStats 上生效。
        /// </summary>
        private static void RefreshIfEquipped(InventorySlot weaponSlot)
        {
            GameObject player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
            WeaponEquipController equip = player != null ? player.GetComponent<WeaponEquipController>() : null;
            if (equip != null && equip.CurrentWeaponSlot == weaponSlot)
            {
                equip.RefreshAppliedModifiers();
            }
        }

        /// <summary>
        /// 强化只改变槽位上的 WeaponData，不增删槽位本身，但 UI（背包详情/强化详情）
        /// 仍依赖 OnInventoryChanged 来刷新显示，因此强化/重铸成功后主动广播一次。
        /// </summary>
        private static void NotifyInventoryChanged()
        {
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null) return;
            EventBus.Raise(new OnInventoryChanged { Slots = inventory.slots });
        }

        private void EnsureWordIndex()
        {
            if (_wordById != null) return;
            _wordById = DatabaseIndexer.BuildById(
                wordDatabase != null ? wordDatabase.Words : null,
                w => w.ID,
                "WeaponEnhanceSystem");
        }
    }
}
