using System.Collections.Generic;
using IndieGame.Gameplay.Dialogue;
using IndieGame.Gameplay.Stats;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 强化界面文案拼接工具：把 WordSO 的数值加成/特殊效果数据格式化成可展示文字。
    /// 纯展示层工具，不参与任何数值计算或战斗结算。
    /// </summary>
    internal static class WeaponEnhanceTextFormatter
    {
        public static string BuildEffectSummary(WordSO word)
        {
            if (word == null) return string.Empty;

            List<string> parts = new List<string>();

            IReadOnlyList<StatModifierData> modifiers = word.PrefixModifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                parts.Add(FormatModifier(modifiers[i]));
            }

            IReadOnlyList<PrefixSpecialEffect> effects = word.SpecialEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                parts.Add(FormatSpecialEffect(effects[i]));
            }

            return string.Join("，", parts);
        }

        private static string FormatModifier(StatModifierData modifier)
        {
            string sign = modifier.Value >= 0 ? "+" : string.Empty;
            return $"{GetStatTypeLabel(modifier.Type)}{sign}{modifier.Value:0.#}";
        }

        private static string FormatSpecialEffect(PrefixSpecialEffect effect)
        {
            return $"{GetSpecialEffectLabel(effect.Type)} {effect.Value:0.#}";
        }

        private static string GetStatTypeLabel(StatType type)
        {
            switch (type)
            {
                case StatType.Attack: return "攻击";
                case StatType.Defense: return "防御";
                case StatType.Resistance: return "抗性";
                case StatType.MoveSpeed: return "移速";
                case StatType.Luck: return "幸运";
                case StatType.HP: return "HP";
                case StatType.ChargeRate: return "充能速率";
                default: return type.ToString();
            }
        }

        private static string GetSpecialEffectLabel(PrefixSpecialEffectType type)
        {
            switch (type)
            {
                case PrefixSpecialEffectType.Bleed: return "出血几率";
                case PrefixSpecialEffectType.Poison: return "中毒几率";
                case PrefixSpecialEffectType.Crit: return "暴击提升";
                case PrefixSpecialEffectType.AttackSpeed: return "攻速提升";
                case PrefixSpecialEffectType.ChargeSpeed: return "蓄力提升";
                case PrefixSpecialEffectType.StatusResistance: return "异常抗性";
                default: return type.ToString();
            }
        }
    }
}
