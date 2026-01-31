using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.Localization;
using IndieGame.Core;

namespace IndieGame.UI
{
    /// <summary>
    /// 棋盘菜单按钮：
    /// 负责单个按钮的显示与鼠标交互（悬停/点击/移出）。
    /// </summary>
    public class BoardActionButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
    {
        [Header("UI")]
        // 图标
        public Image iconImage;
        // 文本
        public TMP_Text label;

        // --- 内部状态 ---
        private int _index;
        /// <summary>
        /// 初始化按钮显示内容。
        /// </summary>
        /// <param name="name">本地化名称</param>
        /// <param name="icon">图标</param>
        /// <param name="index">按钮索引</param>
        public void Setup(LocalizedString name, Sprite icon, int index)
        {
            _index = index;

            if (label != null)
            {
                if (name == null)
                {
                    label.text = string.Empty;
                }
                else
                {
                    // 异步获取本地化文本，避免阻塞主线程
                    var handle = name.GetLocalizedStringAsync();
                    handle.Completed += op =>
                    {
                        if (label == null) return;
                        label.text = op.Result;
                    };
                }
            }
            // 设置图标
            if (iconImage != null) iconImage.sprite = icon;
        }

        /// <summary>
        /// 设置选中状态：用缩放动画强调选中效果。
        /// </summary>
        /// <param name="selected">是否选中</param>
        /// <param name="scale">目标缩放值</param>
        /// <param name="duration">过渡时长</param>
        public void SetSelected(bool selected, float scale, float duration)
        {
            transform.DOKill();
            transform.DOScale(scale, duration);
        }

        /// <summary>
        /// 鼠标进入：触发 Hover 回调。
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            EventBus.Raise(new BoardActionButtonHoverEvent { Index = _index });
        }

        /// <summary>
        /// 鼠标点击：触发 Click 回调。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            EventBus.Raise(new BoardActionButtonClickEvent { Index = _index });
        }

        /// <summary>
        /// 鼠标移出：触发 Exit 回调。
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            EventBus.Raise(new BoardActionButtonExitEvent { Index = _index });
        }
    }
}
