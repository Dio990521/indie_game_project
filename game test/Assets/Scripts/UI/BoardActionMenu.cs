using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.UI
{
    [Serializable]
    public class MenuOption
    {
        public string Name;
        public Sprite Icon;
        public Action<Action> Callback;
    }

    public class BoardActionMenu : MonoBehaviour
    {
        [Header("Dependencies")]
        public GameInputReader inputReader;
        public BoardActionButton buttonPrefab;

        [Header("Layout")]
        public float radius = 120f;
        public float arcAngle = 90f;
        public Vector2 offset = new Vector2(120f, 40f);

        [Header("Selection")]
        public float selectedScale = 1.15f;
        public float normalScale = 1f;
        public float selectTweenDuration = 0.12f;
        public float inputRepeatDelay = 0.2f;

        [Header("Animation")]
        public float showDuration = 0.25f;
        public float hideDuration = 0.18f;
        public float showStagger = 0.06f;
        public Ease showEase = Ease.OutBack;
        public Ease hideEase = Ease.InBack;

        private readonly List<MenuOption> _options = new List<MenuOption>();
        private readonly List<BoardActionButton> _buttons = new List<BoardActionButton>();
        private int _selectedIndex = 0;
        private float _nextInputTime = 0f;
        private Sequence _showSequence;
        private Sequence _hideSequence;
        private Transform _cameraTransform;

        private void Awake()
        {
            if (Camera.main != null) _cameraTransform = Camera.main.transform;
        }

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode)
            {
                Show();
            }
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            if (inputReader != null) inputReader.MoveEvent += OnMoveInput;
            if (inputReader != null) inputReader.InteractEvent += OnInteractInput;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            if (inputReader != null) inputReader.MoveEvent -= OnMoveInput;
            if (inputReader != null) inputReader.InteractEvent -= OnInteractInput;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null && Camera.main != null) _cameraTransform = Camera.main.transform;
            if (_cameraTransform == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position, Vector3.up);
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.BoardMode)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        public void Show()
        {
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;

            gameObject.SetActive(true);
            BuildDefaultOptions();
            RebuildButtons();
            LayoutButtons();
            SelectIndex(0, instant: true);
            PlayShowAnimation();
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            PlayHideAnimation();
        }

        private void BuildDefaultOptions()
        {
            _options.Clear();
            _options.Add(new MenuOption
            {
                Name = "Roll Dice",
                Icon = null,
                Callback = done =>
                {
                    BoardGameManager.Instance.RollDice();
                    done?.Invoke();
                }
            });
            _options.Add(new MenuOption
            {
                Name = "Item",
                Icon = null,
                Callback = done =>
                {
                    Debug.Log("[BoardActionMenu] Item clicked.");
                    done?.Invoke();
                }
            });
            _options.Add(new MenuOption
            {
                Name = "Camp",
                Icon = null,
                Callback = done =>
                {
                    Debug.Log("[BoardActionMenu] Camp clicked.");
                    done?.Invoke();
                }
            });
        }

        private void RebuildButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null) Destroy(_buttons[i].gameObject);
            }
            _buttons.Clear();

            for (int i = 0; i < _options.Count; i++)
            {
                BoardActionButton button = Instantiate(buttonPrefab, transform);
                button.Setup(_options[i], i, OnButtonHover, OnButtonClick);
                _buttons.Add(button);
            }
        }

        private void LayoutButtons()
        {
            int count = _buttons.Count;
            if (count == 0) return;

            float startAngle = -arcAngle * 0.5f;
            float step = count > 1 ? arcAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius) + offset;

                RectTransform rt = _buttons[i].GetComponent<RectTransform>();
                rt.anchoredPosition = pos;
            }
        }

        private void OnMoveInput(Vector2 input)
        {
            if (!gameObject.activeInHierarchy) return;
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            if (Time.time < _nextInputTime) return;

            if (input.y > 0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                SelectIndex((_selectedIndex - 1 + _buttons.Count) % _buttons.Count);
            }
            else if (input.y < -0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                SelectIndex((_selectedIndex + 1) % _buttons.Count);
            }
        }

        private void OnInteractInput()
        {
            if (!gameObject.activeInHierarchy) return;
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            OnButtonClick(_selectedIndex);
        }

        private void OnButtonHover(int index)
        {
            SelectIndex(index);
        }

        private void OnButtonClick(int index)
        {
            if (index < 0 || index >= _options.Count) return;
            MenuOption option = _options[index];
            option.Callback?.Invoke(() => { });
        }

        private void SelectIndex(int index, bool instant = false)
        {
            if (_buttons.Count == 0) return;
            _selectedIndex = Mathf.Clamp(index, 0, _buttons.Count - 1);

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool isSelected = i == _selectedIndex;
                _buttons[i].SetSelected(isSelected, isSelected ? selectedScale : normalScale, instant ? 0f : selectTweenDuration);
            }
        }

        private void PlayShowAnimation()
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();

            _showSequence = DOTween.Sequence();
            for (int i = 0; i < _buttons.Count; i++)
            {
                Transform t = _buttons[i].transform;
                t.localScale = Vector3.zero;
                _showSequence.Append(t.DOScale(normalScale, showDuration).SetEase(showEase));
                if (i < _buttons.Count - 1) _showSequence.AppendInterval(showStagger);
            }
        }

        private void PlayHideAnimation()
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();

            _hideSequence = DOTween.Sequence();
            for (int i = 0; i < _buttons.Count; i++)
            {
                Transform t = _buttons[i].transform;
                _hideSequence.Join(t.DOScale(0f, hideDuration).SetEase(hideEase));
            }
            _hideSequence.OnComplete(() => gameObject.SetActive(false));
        }
    }
}
