using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using IndieGame.Core;

namespace IndieGame.UI.Town
{
    /// <summary>
    /// 城镇动作按钮：
    /// 参照 CampActionButton 的交互模式，通过 EventBus 派发 Hover/Click/Exit 事件，
    /// 以保持 UI 层与业务逻辑的解耦。
    /// </summary>
    public class TownActionButton : MonoBehaviour,
        IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
    {
        [Header("UI")]
        // 图标
        [SerializeField] private Image iconImage;
        // 文本标签
        [SerializeField] private TMP_Text label;

        // 按钮在当前菜单中的索引
        private int _index;

        /// <summary>
        /// 初始化按钮的显示内容。
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="icon">图标（可为 null）</param>
        /// <param name="index">索引，与 TownUIView._buttonDefs 一一对应</param>
        public void Setup(string displayName, Sprite icon, int index)
        {
            _index = index;
            if (label != null)
                label.text = string.IsNullOrEmpty(displayName) ? string.Empty : displayName;
            if (iconImage != null)
                iconImage.sprite = icon;
        }

        /// <summary>
        /// 设置选中状态，用缩放动画强调高亮效果。
        /// </summary>
        public void SetSelected(bool selected, float scale, float duration)
        {
            transform.DOKill();
            transform.DOScale(scale, duration);
        }

        /// <summary>鼠标进入：触发 Hover 事件。</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            EventBus.Raise(new TownActionButtonHoverEvent { Index = _index });
        }

        /// <summary>鼠标点击：触发 Click 事件。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            EventBus.Raise(new TownActionButtonClickEvent { Index = _index });
        }

        /// <summary>鼠标移出：触发 Exit 事件。</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            EventBus.Raise(new TownActionButtonExitEvent { Index = _index });
        }
    }
}
