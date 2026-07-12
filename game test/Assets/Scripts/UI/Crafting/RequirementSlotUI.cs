using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Crafting;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 右侧“材料清单”单行 UI：
    /// 展示每条需求的图标、名称、数量进度（拥有/需求）。
    /// </summary>
    public class RequirementSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text amountText;
        [Tooltip("独立展示背包持有总量的文本（如“持有: 45”），与需求进度 amountText 分开显示")]
        [SerializeField] private TMP_Text ownedText;

        [Header("Style")]
        [SerializeField] private Color enoughColor = new Color(0.45f, 0.95f, 0.45f);
        [SerializeField] private Color lackingColor = new Color(1f, 0.45f, 0.45f);

        /// <summary>
        /// 刷新单条材料需求展示。
        /// </summary>
        public void Setup(BlueprintRequirement requirement, int ownedCount)
        {
            if (requirement == null)
            {
                if (iconImage != null) iconImage.enabled = false;
                if (nameText != null) nameText.text = "Invalid Requirement";
                if (amountText != null) amountText.text = "0/0";
                if (ownedText != null) ownedText.text = "持有: 0";
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite = requirement.Item != null ? requirement.Item.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (nameText != null)
            {
                // 材料名优先使用 ItemSO 的本地化名称；无配置时回退 ID
                if (requirement.Item != null && requirement.Item.ItemName != null)
                {
                    nameText.text = requirement.Item.ItemName.GetLocalizedString();
                }
                else if (requirement.Item != null)
                {
                    nameText.text = string.IsNullOrWhiteSpace(requirement.Item.ID) ? "Unknown Item" : requirement.Item.ID;
                }
                else
                {
                    nameText.text = "Unknown Item";
                }
            }

            int need = requirement.Amount;
            int have = Mathf.Max(0, ownedCount);
            bool enough = have >= need;

            if (amountText != null)
            {
                // 需求进度：封顶显示为 need，避免持有远超需求时进度数字失真（如 45/12）
                int progress = Mathf.Min(have, need);
                amountText.color = enough ? enoughColor : lackingColor;
                amountText.text = $"{progress}/{need}";
            }

            if (ownedText != null)
            {
                ownedText.text = $"持有: {have}";
            }
        }
    }
}
