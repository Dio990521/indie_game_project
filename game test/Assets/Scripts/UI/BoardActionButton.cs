using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

namespace IndieGame.UI
{
    public class BoardActionButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("UI")]
        public Image iconImage;
        public Text label;

        private int _index;
        private Action<int> _onHover;
        private Action<int> _onClick;

        public void Setup(MenuOption option, int index, Action<int> onHover, Action<int> onClick)
        {
            _index = index;
            _onHover = onHover;
            _onClick = onClick;

            if (label != null) label.text = option.Name;
            if (iconImage != null) iconImage.sprite = option.Icon;
        }

        public void SetSelected(bool selected, float scale, float duration)
        {
            transform.DOKill();
            transform.DOScale(scale, duration);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onHover?.Invoke(_index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke(_index);
        }
    }
}
