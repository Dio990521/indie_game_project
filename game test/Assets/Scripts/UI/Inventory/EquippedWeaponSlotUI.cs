using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IndieGame.Gameplay.Inventory;
using IndieGame.UI.Common;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// "当前装备武器" UI 槽：
    /// 显示当前装备的 WeaponSO 图标与名称；装备后武器会从背包消失，
    /// 因此点击该槽是卸下武器的唯一入口（未装备时点击无效果）。
    /// </summary>
    public class EquippedWeaponSlotUI : BaseSlotUI
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private string emptyDisplayName = "未装备武器";

        // 点击回调（由外部注入，通常是"卸下武器"）；未装备时为 null，点击不产生效果
        private Action _onClick;

        /// <summary>
        /// 刷新槽位显示内容。
        /// </summary>
        /// <param name="weapon">当前装备的武器，未装备时传 null</param>
        /// <param name="onClick">点击回调（卸下武器），未装备时传 null</param>
        public void Refresh(WeaponSO weapon, Action onClick)
        {
            _onClick = onClick;

            SetIcon(iconImage, weapon?.Icon);
            SetName(nameText, weapon?.GetLocalizedName(), emptyDisplayName);
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            _onClick?.Invoke();
        }
    }
}
