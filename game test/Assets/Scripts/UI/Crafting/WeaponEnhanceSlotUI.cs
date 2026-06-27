using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using IndieGame.Core;
using IndieGame.UI.Common;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 强化界面左侧"武器列表"槽位 UI：显示图标+名称+"已装备"角标，点击广播 EntryKey。
    /// </summary>
    public class WeaponEnhanceSlotUI : BaseSlotUI
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject equippedBadge;

        private string _entryKey;

        public void Setup(string entryKey, Sprite icon, string displayName, bool isEquipped)
        {
            _entryKey = string.IsNullOrWhiteSpace(entryKey) ? string.Empty : entryKey;

            SetIcon(iconImage, icon);
            SetName(nameText, displayName, "Unknown Weapon");

            if (equippedBadge != null) equippedBadge.SetActive(isEquipped);
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_entryKey)) return;
            EventBus.Raise(new WeaponEnhanceSlotClickedEvent { EntryKey = _entryKey });
        }
    }
}
