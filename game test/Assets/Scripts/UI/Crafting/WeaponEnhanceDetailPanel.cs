using System.Collections.Generic;
using System.Text;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Dialogue;
using IndieGame.Gameplay.Equipment;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Stats;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 强化界面右侧详情面板管理器（纯 C# 辅助类）：
    /// 职责：武器基础信息展示、已应用前缀列表、强化/重铸按钮状态与消耗文案。
    /// 由 CraftingUIController 在 Awake 中实例化并持有。
    /// </summary>
    internal class WeaponEnhanceDetailPanel
    {
        private GameObjectPool _appliedPrefixPool;
        private CraftUIBinder _binder;
        private readonly List<AppliedPrefixSlotUI> _activeAppliedPrefixSlots = new List<AppliedPrefixSlotUI>();

        public void Init(CraftUIBinder binder, int appliedPrefixPoolWarmup)
        {
            _binder = binder;
            if (binder.AppliedPrefixSlotPrefab != null && binder.AppliedPrefixListRoot != null)
                _appliedPrefixPool = new GameObjectPool(binder.AppliedPrefixSlotPrefab, binder.AppliedPrefixListRoot, appliedPrefixPoolWarmup);
        }

        /// <summary>
        /// 展示选中武器的基础信息 + 已应用前缀列表。
        /// </summary>
        public void ShowForWeapon(InventorySlot weaponSlot, WeaponEnhanceSystem enhanceSystem, int selectedRebindIndex)
        {
            WeaponSO weapon = weaponSlot?.Item as WeaponSO;
            if (weapon == null)
            {
                EnterEmptyState();
                return;
            }

            if (_binder.EnhanceEmptyStateNode != null) _binder.EnhanceEmptyStateNode.SetActive(false);
            if (_binder.EnhanceDetailNode != null) _binder.EnhanceDetailNode.SetActive(true);

            // 当前 WeaponSO 没有单独的"种类"字段，先用固定文案占位，后续可扩展
            if (_binder.WeaponKindText != null) _binder.WeaponKindText.text = "武器";

            if (enhanceSystem != null)
            {
                enhanceSystem.ComposeDisplayName(weapon, weaponSlot.CustomName, weaponSlot.WeaponData, displayName =>
                {
                    if (_binder.WeaponNameText != null) _binder.WeaponNameText.text = displayName;
                });
            }
            else if (_binder.WeaponNameText != null)
            {
                _binder.WeaponNameText.text = !string.IsNullOrWhiteSpace(weaponSlot.CustomName) ? weaponSlot.CustomName : weapon.GetLocalizedName();
            }

            SetStatText(_binder.BaseHPText, weapon.Modifiers, StatType.HP);
            SetStatText(_binder.BaseAttackText, weapon.Modifiers, StatType.Attack);
            SetStatText(_binder.BaseDefenseText, weapon.Modifiers, StatType.Defense);
            SetStatText(_binder.BaseChargeRateText, weapon.Modifiers, StatType.ChargeRate);

            RefreshAppliedPrefixList(weaponSlot, enhanceSystem, selectedRebindIndex);
        }

        /// <summary>
        /// 空态：未选中任何武器（或左侧列表为空）。
        /// </summary>
        public void EnterEmptyState()
        {
            if (_binder.EnhanceEmptyStateNode != null) _binder.EnhanceEmptyStateNode.SetActive(true);
            if (_binder.EnhanceDetailNode != null) _binder.EnhanceDetailNode.SetActive(false);
            ReleaseAllAppliedPrefixSlots();
        }

        public void RefreshAppliedPrefixList(InventorySlot weaponSlot, WeaponEnhanceSystem enhanceSystem, int selectedRebindIndex)
        {
            ReleaseAllAppliedPrefixSlots();
            if (weaponSlot?.WeaponData == null || enhanceSystem == null) return;

            List<string> prefixIds = weaponSlot.WeaponData.AppliedPrefixWordIds;
            for (int i = 0; i < prefixIds.Count; i++)
            {
                WordSO word = enhanceSystem.GetWord(prefixIds[i]);
                if (word == null) continue;

                AppliedPrefixSlotUI ui = SpawnAppliedPrefixSlot();
                if (ui == null) continue;

                string displayName = word.DisplayName != null ? word.DisplayName.GetLocalizedString() : word.ID;
                string effectSummary = WeaponEnhanceTextFormatter.BuildEffectSummary(word);
                ui.Setup(i, displayName, effectSummary, i == selectedRebindIndex);
                _activeAppliedPrefixSlots.Add(ui);
            }
        }

        /// <summary>
        /// 刷新"强化"按钮的可用状态与消耗文案。
        /// </summary>
        public void RefreshEnhanceButtonState(WeaponEnhanceSystem enhanceSystem, InventorySlot weaponSlot, string selectedWordId)
        {
            if (_binder.EnhanceConfirmButton == null) return;

            bool canEnhance = enhanceSystem != null && weaponSlot != null && !string.IsNullOrWhiteSpace(selectedWordId)
                && enhanceSystem.CanEnhance(weaponSlot, selectedWordId, out _);
            _binder.EnhanceConfirmButton.interactable = canEnhance;

            if (_binder.EnhanceCostText == null) return;
            int prefixCount = weaponSlot?.WeaponData?.AppliedPrefixWordIds.Count ?? 0;
            IReadOnlyList<BlueprintRequirement> cost = enhanceSystem?.GetEnhanceCost(prefixCount);
            _binder.EnhanceCostText.text = BuildCostText(cost);
        }

        /// <summary>
        /// 刷新"重铸"按钮的可用状态与消耗文案。
        /// </summary>
        public void RefreshRebindButtonState(WeaponEnhanceSystem enhanceSystem, InventorySlot weaponSlot, int rebindIndex, string selectedWordId)
        {
            if (_binder.RebindConfirmButton == null) return;

            bool canRebind = enhanceSystem != null && weaponSlot != null && rebindIndex >= 0 && !string.IsNullOrWhiteSpace(selectedWordId)
                && enhanceSystem.CanRebind(weaponSlot, rebindIndex, selectedWordId, out _);
            _binder.RebindConfirmButton.interactable = canRebind;

            if (_binder.RebindCostText == null) return;
            IReadOnlyList<BlueprintRequirement> cost = rebindIndex >= 0 ? enhanceSystem?.GetRebindCost(rebindIndex) : null;
            _binder.RebindCostText.text = BuildCostText(cost);
        }

        public void ReleaseAllAppliedPrefixSlots()
        {
            for (int i = 0; i < _activeAppliedPrefixSlots.Count; i++)
            {
                if (_activeAppliedPrefixSlots[i] != null && _appliedPrefixPool != null)
                    _appliedPrefixPool.Release(_activeAppliedPrefixSlots[i].gameObject);
            }
            _activeAppliedPrefixSlots.Clear();
        }

        // --- 私有方法 ---

        private AppliedPrefixSlotUI SpawnAppliedPrefixSlot()
        {
            if (_appliedPrefixPool == null || _binder.AppliedPrefixListRoot == null) return null;

            GameObject go = _appliedPrefixPool.Get();
            go.transform.SetParent(_binder.AppliedPrefixListRoot, false);

            AppliedPrefixSlotUI ui = go.GetComponent<AppliedPrefixSlotUI>();
            if (ui == null)
            {
                DebugTools.LogError("[WeaponEnhanceDetailPanel] appliedPrefixSlotPrefab 缺少 AppliedPrefixSlotUI 组件。");
                _appliedPrefixPool.Release(go);
                return null;
            }
            return ui;
        }

        private static void SetStatText(TMPro.TMP_Text label, List<StatModifierData> modifiers, StatType type)
        {
            if (label == null) return;

            float value = 0f;
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    if (modifiers[i].Type != type) continue;
                    value = modifiers[i].Value;
                    break;
                }
            }
            label.text = value.ToString("0.#");
        }

        private static string BuildCostText(IReadOnlyList<BlueprintRequirement> cost)
        {
            if (cost == null || cost.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < cost.Count; i++)
            {
                BlueprintRequirement requirement = cost[i];
                if (requirement?.Item == null) continue;
                if (sb.Length > 0) sb.Append("，");
                sb.Append(requirement.Item.GetLocalizedName()).Append(" x").Append(requirement.Amount);
            }
            return sb.ToString();
        }
    }
}
