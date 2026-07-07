using UnityEngine;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 稀有度配色/显示名工具：背包格子背景色与详情面板稀有度色块共用同一份映射，保证颜色一致。
    /// 当前为占位色值（无美术资源），后续若需要美术/设计师在 Inspector 调色，可改造为 ScriptableObject 配置。
    /// </summary>
    public static class ItemRarityUtility
    {
        public static Color GetColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common    => new Color(0.78f, 0.78f, 0.78f), // 灰白
                ItemRarity.Uncommon  => new Color(0.30f, 0.75f, 0.35f), // 绿
                ItemRarity.Rare      => new Color(0.25f, 0.55f, 0.95f), // 蓝
                ItemRarity.Epic      => new Color(0.62f, 0.32f, 0.85f), // 紫
                ItemRarity.Legendary => new Color(0.95f, 0.60f, 0.15f), // 橙金
                _                    => Color.white
            };
        }

        public static string GetDisplayName(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common    => "普通",
                ItemRarity.Uncommon  => "优良",
                ItemRarity.Rare      => "稀有",
                ItemRarity.Epic      => "史诗",
                ItemRarity.Legendary => "传说",
                _                    => "未知"
            };
        }
    }
}
