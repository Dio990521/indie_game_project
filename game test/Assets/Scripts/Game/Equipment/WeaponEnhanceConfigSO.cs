using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Crafting;

namespace IndieGame.Gameplay.Equipment
{
    /// <summary>
    /// 武器强化/重铸的材料消耗配置（ScriptableObject）：
    /// 按"第几次强化/重铸"逐级配置消耗，越级递增由策划在 Inspector 里直接填写，不走公式。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Equipment/Weapon Enhance Config")]
    public class WeaponEnhanceConfigSO : ScriptableObject
    {
        [Tooltip("强化消耗：index 0 = 第 1 次强化（已有0个前缀时）的消耗，index 1 = 第 2 次……")]
        [SerializeField] private List<WeaponEnhanceCostStep> enhanceCostBySteps = new List<WeaponEnhanceCostStep>();

        [Tooltip("重铸消耗：index 对应被替换的前缀位序号（0~4）")]
        [SerializeField] private List<WeaponEnhanceCostStep> rebindCostBySteps = new List<WeaponEnhanceCostStep>();

        /// <summary>
        /// 获取第 stepIndex 次强化所需材料；超出配置范围时回退到最后一条配置。
        /// </summary>
        public IReadOnlyList<BlueprintRequirement> GetEnhanceCost(int stepIndex)
        {
            return GetCost(enhanceCostBySteps, stepIndex);
        }

        /// <summary>
        /// 获取替换第 stepIndex 个前缀位所需材料；超出配置范围时回退到最后一条配置。
        /// </summary>
        public IReadOnlyList<BlueprintRequirement> GetRebindCost(int stepIndex)
        {
            return GetCost(rebindCostBySteps, stepIndex);
        }

        private static IReadOnlyList<BlueprintRequirement> GetCost(List<WeaponEnhanceCostStep> steps, int stepIndex)
        {
            if (steps == null || steps.Count == 0) return System.Array.Empty<BlueprintRequirement>();

            int clampedIndex = Mathf.Clamp(stepIndex, 0, steps.Count - 1);
            WeaponEnhanceCostStep step = steps[clampedIndex];
            return step?.Materials ?? (IReadOnlyList<BlueprintRequirement>)System.Array.Empty<BlueprintRequirement>();
        }
    }
}
