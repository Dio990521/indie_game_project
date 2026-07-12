using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 右侧“打造效果”预览单行 UI：
    /// 展示一条属性加成的图标、名称，以及数值（未打造过时显示“~?”，已打造过则显示真实数值）。
    /// </summary>
    public class CraftEffectSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text valueText;

        /// <summary>
        /// 刷新单条打造效果展示。
        /// </summary>
        /// <param name="statName">属性显示名（如“物理攻击”）</param>
        /// <param name="value">属性数值（来自 WeaponSO/ArmorSO 的 Modifiers，固定值，不做随机）</param>
        /// <param name="isRevealed">是否已揭晓：false 时忽略 value，只显示“~?”</param>
        /// <param name="icon">可选图标</param>
        public void Setup(string statName, float value, bool isRevealed, Sprite icon = null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (nameText != null)
                nameText.text = string.IsNullOrWhiteSpace(statName) ? "Unknown" : statName;

            if (valueText != null)
                valueText.text = isRevealed ? $"~ {value:0.##}" : "~ ?";
        }
    }
}
