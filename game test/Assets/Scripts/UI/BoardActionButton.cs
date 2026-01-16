using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

namespace IndieGame.UI
{
    public class BoardActionButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler
    {
        [Header("UI")]
        public Image iconImage;
        public TMP_Text label;

        private int _index;
        private Action<int> _onHover;
        private Action<int> _onClick;
        private Action<int> _onExit;

        public void Setup(string name, Sprite icon, int index, Action<int> onHover, Action<int> onClick, Action<int> onExit)
        {
            _index = index;
            _onHover = onHover;
            _onClick = onClick;
            _onExit = onExit;

            if (label != null) label.text = name;
            if (iconImage != null) iconImage.sprite = icon;
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

        public void OnPointerExit(PointerEventData eventData)
        {
            _onExit?.Invoke(_index);
        }
    }
}
