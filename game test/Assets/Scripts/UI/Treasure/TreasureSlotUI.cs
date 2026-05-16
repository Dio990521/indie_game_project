using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Gameplay.Treasure;
using IndieGame.UI.Common;

namespace IndieGame.UI.Treasure
{
    /// <summary>
    /// 宝具列表单行 UI 组件：
    /// 显示宝具图标（左）、本地化名称（中）、行动点消耗（右），支持选中高亮切换。
    /// </summary>
    public class TreasureSlotUI : MonoBehaviour
    {
        [Tooltip("宝具图标")]
        [SerializeField] private Image iconImage;

        [Tooltip("宝具名称标签")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("行动点消耗标签，如 \"3 AP\"")]
        [SerializeField] private TMP_Text costLabel;

        [Tooltip("选中时激活的高亮背景对象")]
        [SerializeField] private GameObject highlightObject;

        /// <summary>
        /// 用宝具数据填充本行 UI。
        /// </summary>
        public void Setup(TreasureSO data)
        {
            if (data == null) return;

            // 图标
            if (iconImage != null)
            {
                iconImage.sprite = data.Icon;
                iconImage.enabled = data.Icon != null;
            }

            // 行动点消耗
            if (costLabel != null)
                costLabel.text = $"{data.ActionPointCost} AP";

            // 名称：通过 LocalizedString 异步加载，避免主线程阻塞
            if (nameLabel != null)
            {
                nameLabel.text = string.Empty;
                LocalizedTextHelper.SetLocalizedAsync(nameLabel, data.DisplayName);
            }

            SetHighlighted(false);
        }

        /// <summary>
        /// 切换选中高亮状态。
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (highlightObject != null)
                highlightObject.SetActive(highlighted);
        }

        /// <summary>
        /// 以纯文本填充本行（用于非宝具列表，如斗篷传送目标）。
        /// 隐藏图标和代价标签，只显示传入的文字。
        /// </summary>
        public void SetupSimple(string displayText)
        {
            if (iconImage != null)
            {
                iconImage.sprite  = null;
                iconImage.enabled = false;
            }
            if (nameLabel != null)
                nameLabel.text = displayText;
            if (costLabel != null)
                costLabel.text = string.Empty;

            SetHighlighted(false);
        }
    }
}
