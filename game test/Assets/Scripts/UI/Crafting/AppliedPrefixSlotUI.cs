using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using IndieGame.Core;
using IndieGame.UI.Common;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 强化详情面板"已应用前缀"逐条展示槽位：显示前缀名+效果文案，点击选中作为重铸的替换目标。
    /// </summary>
    public class AppliedPrefixSlotUI : BaseSlotUI
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text effectSummaryText;
        [SerializeField] private GameObject selectedHighlight;

        private int _index;

        public void Setup(int index, string displayName, string effectSummary, bool isSelected)
        {
            _index = index;

            SetName(nameText, displayName, "Unknown Word");
            if (effectSummaryText != null) effectSummaryText.text = effectSummary ?? string.Empty;
            if (selectedHighlight != null) selectedHighlight.SetActive(isSelected);
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            EventBus.Raise(new AppliedPrefixSlotClickedEvent { Index = _index });
        }
    }
}
