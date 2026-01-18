using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Input;

namespace IndieGame.UI
{
    public class BoardActionMenuView : MonoBehaviour
    {
        private enum SelectionSource
        {
            None,
            Keyboard,
            Mouse
        }

        [Header("Binder")]
        [SerializeField] private BoardActionMenuBinder binder;

        [Header("Dependencies")]
        public GameInputReader inputReader;
        public Transform target;

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

        public event Action OnRollDiceRequested;
        public static event Action OnRequestOpenInventory;

        private readonly List<BoardActionOptionData> _options = new List<BoardActionOptionData>();
        private readonly List<BoardActionButton> _buttons = new List<BoardActionButton>();
        private bool _isBuilt = false;
        private int _selectedIndex = 0;
        private float _nextInputTime = 0f;
        private Sequence _showSequence;
        private Sequence _hideSequence;
        private RectTransform _selfRect;
        private RectTransform _canvasRect;
        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;
        private bool _allowShow = true;
        private SelectionSource _selectionSource = SelectionSource.None;

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[BoardActionMenuView] Missing binder reference.");
                return;
            }
            _selfRect = binder.RootRect;
            _canvasGroup = binder.CanvasGroup;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }

        private void Start()
        {
            if (binder != null)
            {
                Transform root = binder.RootRect != null ? binder.RootRect : transform;
                Canvas canvas = root.GetComponentInParent<Canvas>();
                if (canvas != null) _canvasRect = canvas.GetComponent<RectTransform>();
            }

            if (target == null && GameManager.Instance != null)
            {
                target = GameManager.Instance.GetCurrentPlayerTransform();
            }

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
            _showSequence?.Kill();
            _hideSequence?.Kill();
        }

        private void LateUpdate()
        {
            if (_selfRect == null || _canvasRect == null || target == null) return;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out Vector2 localPoint);
            _selfRect.anchoredPosition = localPoint;
        }

        public void SetAllowShow(bool allow)
        {
            _allowShow = allow;
            if (!allow)
            {
                Hide();
                return;
            }

            if (GameManager.Instance.CurrentState == GameState.BoardMode)
            {
                Show();
            }
        }

        public void Show()
        {
            if (!_allowShow || _isVisible) return;
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;

            if (!_isBuilt)
            {
                BuildDefaultData();
                RebuildButtons();
                LayoutButtons();
                _isBuilt = true;
            }
            SelectIndex(0, instant: true);
            PlayShowAnimation();
            _isVisible = true;
        }

        public void Hide()
        {
            if (!_isVisible) return;
            PlayHideAnimation();
        }

        private void HandleStateChanged(GameState newState)
        {
            if (newState == GameState.BoardMode)
            {
                if (_allowShow) Show();
            }
            else
            {
                Hide();
            }
        }

        private void BuildDefaultData()
        {
            _options.Clear();
            _options.Add(new BoardActionOptionData { Id = BoardActionId.RollDice, Name = "Roll Dice" });
            _options.Add(new BoardActionOptionData { Id = BoardActionId.Item, Name = "Item" });
            _options.Add(new BoardActionOptionData { Id = BoardActionId.Camp, Name = "Camp" });
        }

        private void RebuildButtons()
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                {
                    _buttons[i].transform.DOKill(true);
                    Destroy(_buttons[i].gameObject);
                }
            }
            _buttons.Clear();
            _isBuilt = false;

            if (binder.ButtonPrefab == null || binder.ButtonContainer == null) return;
            if (binder.ButtonPrefab.gameObject.scene.IsValid())
            {
                Debug.LogError("[BoardActionMenuView] ButtonPrefab must be a prefab asset, not a scene object.");
                return;
            }
            bool parentValid = binder.ButtonContainer.gameObject.scene.IsValid();
            if (!parentValid)
            {
                Debug.LogWarning("[BoardActionMenuView] ButtonContainer is not a scene object, skipping button rebuild.");
                return;
            }
            for (int i = binder.ButtonContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(binder.ButtonContainer.GetChild(i).gameObject);
            }

            for (int i = 0; i < _options.Count; i++)
            {
                BoardActionOptionData option = _options[i];
                BoardActionButton button = Instantiate(binder.ButtonPrefab);
                if (binder.ButtonContainer == null) break;
                button.transform.SetParent(binder.ButtonContainer, false);
                button.Setup(option.Name, option.Icon, i, OnButtonHover, OnButtonClick, OnButtonExit);
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
            if (!_isVisible) return;
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            if (Time.time < _nextInputTime) return;

            if (input.y > 0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                _selectionSource = SelectionSource.Keyboard;
                SelectIndex((_selectedIndex - 1 + _buttons.Count) % _buttons.Count);
            }
            else if (input.y < -0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                _selectionSource = SelectionSource.Keyboard;
                SelectIndex((_selectedIndex + 1) % _buttons.Count);
            }
        }

        private void OnInteractInput()
        {
            if (!_isVisible) return;
            if (GameManager.Instance.CurrentState != GameState.BoardMode) return;
            OnButtonClick(_selectedIndex);
        }

        private void OnButtonHover(int index)
        {
            _selectionSource = SelectionSource.Mouse;
            SelectIndex(index);
        }

        private void OnButtonExit(int index)
        {
            if (_selectionSource != SelectionSource.Mouse) return;
            ClearSelection();
        }

        private void OnButtonClick(int index)
        {
            if (index < 0 || index >= _options.Count) return;
            BoardActionOptionData option = _options[index];
            switch (option.Id)
            {
                case BoardActionId.RollDice:
                    OnRollDiceRequested?.Invoke();
                    Hide();
                    break;
                case BoardActionId.Item:
                    OnRequestOpenInventory?.Invoke();
                    Hide();
                    break;
                case BoardActionId.Camp:
                    Debug.Log("[BoardActionMenuView] Camp clicked.");
                    break;
            }
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

        private void ClearSelection()
        {
            _selectedIndex = -1;
            _selectionSource = SelectionSource.None;

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].SetSelected(false, normalScale, selectTweenDuration);
            }
        }

        private void PlayShowAnimation()
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }

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
            _hideSequence.OnComplete(() =>
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.blocksRaycasts = false;
                    _canvasGroup.interactable = false;
                }
                _isVisible = false;
            });
        }
    }
}
