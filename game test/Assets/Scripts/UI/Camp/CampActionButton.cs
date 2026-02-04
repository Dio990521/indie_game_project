using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using IndieGame.Core;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营动作按钮：
    /// 参照 BoardActionButton 的交互模式，统一通过 EventBus 派发 Hover/Click/Exit 事件，
    /// 以避免 UI 逻辑直接依赖具体的按钮点击回调。
    /// </summary>
    public class CampActionButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
    {
        [Header("UI")]
        // 图标
        [SerializeField] private Image iconImage;
        // 文本
        [SerializeField] private TMP_Text label;

        // --- 内部状态 ---
        private int _index;

        /// <summary>
        /// 初始化按钮显示内容。
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="icon">图标</param>
        /// <param name="index">按钮索引（与动作列表一一对应）</param>
        public void Setup(string displayName, Sprite icon, int index)
        {
            _index = index;
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(displayName) ? string.Empty : displayName;
            }
            if (iconImage != null)
            {
                iconImage.sprite = icon;
            }
        }

        /// <summary>
        /// 设置选中状态：用缩放动画强调选中效果（可选）。
        /// </summary>
        public void SetSelected(bool selected, float scale, float duration)
        {
            transform.DOKill();
            transform.DOScale(scale, duration);
        }

        /// <summary>
        /// 鼠标进入：触发 Hover 事件。
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            EventBus.Raise(new CampActionButtonHoverEvent { Index = _index });
        }

        /// <summary>
        /// 鼠标点击：触发 Click 事件。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            EventBus.Raise(new CampActionButtonClickEvent { Index = _index });
        }

        /// <summary>
        /// 鼠标移出：触发 Exit 事件。
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            EventBus.Raise(new CampActionButtonExitEvent { Index = _index });
        }
    }
}

