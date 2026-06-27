using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using IndieGame.Core;
using IndieGame.UI.Common;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 强化界面右侧"语料库"词语槽位 UI：显示前缀文字+效果简述，点击广播 EntryKey（WordSO.ID）。
    /// </summary>
    public class WeaponPrefixSlotUI : BaseSlotUI
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text effectSummaryText;
        [SerializeField] private GameObject selectedHighlight;

        private string _entryKey;

        public void Setup(string entryKey, string displayName, string effectSummary, bool isSelected)
        {
            _entryKey = string.IsNullOrWhiteSpace(entryKey) ? string.Empty : entryKey;

            SetName(nameText, displayName, "Unknown Word");
            if (effectSummaryText != null) effectSummaryText.text = effectSummary ?? string.Empty;
            if (selectedHighlight != null) selectedHighlight.SetActive(isSelected);
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_entryKey)) return;
            EventBus.Raise(new WeaponPrefixSlotClickedEvent { EntryKey = _entryKey });
        }
    }
}
