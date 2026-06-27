using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Dialogue;
using IndieGame.Gameplay.Equipment;
using IndieGame.UI.Common;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 强化界面右侧"语料库"列表管理器：数据源 = 所有已解锁且可用于武器强化的词条，
    /// 排除已应用在当前选中武器上的词缀。
    /// </summary>
    internal class WeaponPrefixListManager : BaseListManager<WordSO, WeaponPrefixSlotUI>
    {
        private CraftUIBinder _binder;
        private readonly List<WordSO> _wordsCache = new List<WordSO>();

        public void Init(CraftUIBinder binder, int slotPoolWarmup)
        {
            _binder = binder;
            if (binder.PrefixSlotPrefab != null && binder.PrefixListRoot != null)
                _slotPool = new GameObjectPool(binder.PrefixSlotPrefab, binder.PrefixListRoot, slotPoolWarmup);
        }

        /// <summary>
        /// 重建语料库列表。返回是否存在条目。
        /// </summary>
        public bool Rebuild(WeaponEnhanceSystem enhanceSystem, List<string> excludeWordIds, string selectedWordId)
        {
            ReleaseAll();
            if (enhanceSystem == null) return false;

            enhanceSystem.GetAvailablePrefixWords(_wordsCache);
            for (int i = 0; i < _wordsCache.Count; i++)
            {
                WordSO word = _wordsCache[i];
                if (word == null || string.IsNullOrWhiteSpace(word.ID)) continue;
                if (excludeWordIds != null && excludeWordIds.Contains(word.ID)) continue;

                bool isSelected = string.Equals(word.ID, selectedWordId, System.StringComparison.Ordinal);
                AddEntry(word, isSelected);
            }
            return _entryOrder.Count > 0;
        }

        public WordSO GetSelectedWord()
        {
            if (string.IsNullOrWhiteSpace(SelectedEntryKey)) return null;
            return _entryByKey.TryGetValue(SelectedEntryKey, out WordSO word) ? word : null;
        }

        private void AddEntry(WordSO word, bool isSelected)
        {
            if (_slotPool == null || _binder.PrefixListRoot == null) return;

            GameObject go = _slotPool.Get();
            go.transform.SetParent(_binder.PrefixListRoot, false);

            WeaponPrefixSlotUI slotUI = go.GetComponent<WeaponPrefixSlotUI>();
            if (slotUI == null)
            {
                DebugTools.LogError("[WeaponPrefixListManager] prefixSlotPrefab 缺少 WeaponPrefixSlotUI 组件。");
                _slotPool.Release(go);
                return;
            }

            string displayName = word.DisplayName != null ? word.DisplayName.GetLocalizedString() : word.ID;
            string effectSummary = WeaponEnhanceTextFormatter.BuildEffectSummary(word);

            slotUI.Setup(word.ID, displayName, effectSummary, isSelected);
            RegisterActiveSlot(word.ID, word, slotUI);
        }
    }
}
