using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营动作按钮：
    /// 仅负责显示内容与选中态缩放动画。菜单只支持键盘操作，
    /// 因此本组件不再实现任何鼠标指针事件（点击/悬停/移出），
    /// 选中与确认统一由 CampUIView 通过方向键 / 交互键驱动。
    /// </summary>
    public class CampActionButton : MonoBehaviour
    {
        [Header("UI")]
        // 图标
        [SerializeField] private Image iconImage;
        // 文本
        [SerializeField] private TMP_Text label;

        /// <summary>
        /// 初始化按钮显示内容。
        /// </summary>
        /// <param name="displayName">显示名称</param>
        /// <param name="icon">图标</param>
        public void Setup(string displayName, Sprite icon)
        {
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
        /// 设置选中状态：用缩放动画强调选中效果。
        /// </summary>
        public void SetSelected(bool selected, float scale, float duration)
        {
            transform.DOKill();
            transform.DOScale(scale, duration);
        }
    }
}
