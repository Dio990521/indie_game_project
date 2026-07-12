using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IndieGame.Gameplay.Inventory;
using IndieGame.UI.Common;

namespace IndieGame.UI.Equipment
{
    /// <summary>
    /// "当前已装备"通用槽位 UI：
    /// 显示某个部位（武器/防具/配方）当前装备的 ItemSO 图标与名称；点击该槽是卸下的入口
    /// （未装备时点击无效果）。与背包的 EquippedWeaponSlotUI 是同一种用途，但不绑定具体子类型，
    /// 武器/防具/配方三个槽位共用这一个组件，避免为每个部位各写一份几乎相同的类。
    /// </summary>
    public class EquippedItemSlotUI : BaseSlotUI
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private string emptyDisplayName = "未装备";

        // 点击回调（由外部注入，通常是"卸下"）；未装备时为 null，点击不产生效果
        private Action _onClick;

        /// <summary>
        /// 刷新槽位显示内容。
        /// </summary>
        /// <param name="item">当前装备的物品，未装备时传 null</param>
        /// <param name="onClick">点击回调（卸下），未装备时传 null</param>
        public void Refresh(ItemSO item, Action onClick)
        {
            _onClick = onClick;

            SetIcon(iconImage, item?.Icon);
            SetName(nameText, item?.GetLocalizedName(), emptyDisplayName);
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            _onClick?.Invoke();
        }
    }
}
