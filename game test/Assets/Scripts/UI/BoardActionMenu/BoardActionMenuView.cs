using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.Runtime.States;

namespace IndieGame.UI
{
    /// <summary>
    /// 棋盘操作菜单视图：
    /// 负责菜单的显示/隐藏、按钮布局、输入响应以及点击逻辑分发。
    /// </summary>
    public class BoardActionMenuView : MonoBehaviour
    {
        private enum SelectionSource
        {
            None,
            Keyboard,
            Mouse
        }

        [Header("Binder")]
        // 绑定器：集中保存 UI 引用
        [SerializeField] private BoardActionMenuBinder binder;

        [Header("Dependencies")]
        // 追踪目标（菜单会围绕该目标投影位置展示，通常为玩家）
        public Transform target;
        [Tooltip("露营场景对应的 LocationID（在 Inspector 中配置）")]
        [SerializeField] private LocationID campingLocationId;

        [Header("Layout")]
        // 按钮半径（围绕目标点的距离）
        public float radius = 120f;
        // 弧形角度范围
        public float arcAngle = 90f;
        // 屏幕偏移
        public Vector2 offset = new Vector2(120f, 40f);

        [Header("Selection")]
        // 选中缩放倍数
        public float selectedScale = 1.15f;
        // 普通缩放
        public float normalScale = 1f;
        // 选中切换动画时长
        public float selectTweenDuration = 0.12f;
        // 输入连发间隔
        public float inputRepeatDelay = 0.2f;

        [Header("Animation")]
        // 显示动画时长
        public float showDuration = 0.25f;
        // 隐藏动画时长
        public float hideDuration = 0.18f;
        // 显示的逐个错帧间隔
        public float showStagger = 0.06f;
        // 显示缓动
        public Ease showEase = Ease.OutBack;
        // 隐藏缓动
        public Ease hideEase = Ease.InBack;

        // --- 运行时数据 ---
        private readonly List<BoardActionOptionData> _options = new List<BoardActionOptionData>();
        private readonly List<BoardActionButton> _buttons = new List<BoardActionButton>();
        private int _selectedIndex = -1;
        private float _nextInputTime = 0f;
        private Sequence _showSequence;
        private Sequence _hideSequence;
        private RectTransform _selfRect;
        private RectTransform _canvasRect;
        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;
        private bool _inputSubscribed = false;
        private SelectionSource _selectionSource = SelectionSource.None;
        private Camera _mainCam;

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[BoardActionMenuView] Missing binder reference.");
                return;
            }
            // 缓存根节点与 CanvasGroup
            _selfRect = binder.RootRect;
            _canvasGroup = binder.CanvasGroup;
            if (_canvasGroup != null)
            {
                // 初始隐藏
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }

        private void OnEnable()
        {
        }

        private void Start()
        {
            if (binder != null)
            {
                Transform root = binder.RootRect != null ? binder.RootRect : transform;
                Canvas canvas = root.GetComponentInParent<Canvas>();
                if (canvas != null) _canvasRect = canvas.GetComponent<RectTransform>();
            }
            // 缓存主相机
            _mainCam = Camera.main;
            // 默认跟随玩家
            target = GameManager.Instance.CurrentPlayer.transform;
        }


        private void OnDisable()
        {
            // 清理输入订阅与动画序列
            UnsubscribeInput();
            _showSequence?.Kill();
            _hideSequence?.Kill();
        }

        private void LateUpdate()
        {
            // 将菜单锚点对齐到目标世界坐标的屏幕投影
            if (_selfRect == null || _canvasRect == null || target == null) return;
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out Vector2 localPoint);
            _selfRect.anchoredPosition = localPoint;
        }

        /// <summary>
        /// 显示菜单并注入按钮数据。
        /// </summary>
        public void Show(List<BoardActionOptionData> data)
        {
            if (_isVisible) return;
            if (data == null || data.Count == 0) return;

            _options.Clear();
            _options.AddRange(data);
            RefreshButtons(data);
            LayoutButtons();
            SelectIndex(0, instant: true);
            PlayShowAnimation();
            _isVisible = true;
            SubscribeInput();
        }

        /// <summary>
        /// 隐藏菜单。
        /// </summary>
        public void Hide()
        {
            if (!_isVisible) return;
            UnsubscribeInput();
            PlayHideAnimation();
        }

        /// <summary>
        /// 根据数据刷新按钮池与显示内容。
        /// </summary>
        private void RefreshButtons(List<BoardActionOptionData> data)
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();

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
            EnsurePoolFromChildren();

            for (int i = 0; i < data.Count; i++)
            {
                BoardActionButton button = GetOrCreateButton(i);
                if (button == null) break;
                BoardActionOptionData option = data[i];
                // 按钮仅负责自身显示，交互通过 EventBus 广播
                button.Setup(option.Name, option.Icon, i);
                button.gameObject.SetActive(true);
            }

            // 关闭多余按钮
            for (int i = data.Count; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                {
                    _buttons[i].gameObject.SetActive(false);
                }
            }
            _selectedIndex = -1;
            _selectionSource = SelectionSource.None;
        }

        /// <summary>
        /// 计算并设置按钮在弧形布局中的位置。
        /// </summary>
        private void LayoutButtons()
        {
            int activeCount = _options.Count;
            if (activeCount == 0) return;

            float startAngle = -arcAngle * 0.5f;
            float step = activeCount > 1 ? arcAngle / (activeCount - 1) : 0f;

            int activeIndex = 0;
            for (int i = 0; i < _buttons.Count; i++)
            {
                BoardActionButton button = _buttons[i];
                if (button == null || !button.gameObject.activeSelf) continue;
                float angle = startAngle + step * activeIndex;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius) + offset;

                RectTransform rt = button.GetComponent<RectTransform>();
                rt.anchoredPosition = pos;
                activeIndex++;
            }
        }

        /// <summary>
        /// 处理移动输入（上下切换选项）。
        /// </summary>
        private void OnMoveInput(InputMoveEvent evt)
        {
            Vector2 input = evt.Value;
            if (Time.time < _nextInputTime) return;
            if (_options.Count == 0) return;

            if (input.y > 0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                _selectionSource = SelectionSource.Keyboard;
                SelectIndex((_selectedIndex - 1 + _options.Count) % _options.Count);
            }
            else if (input.y < -0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                _selectionSource = SelectionSource.Keyboard;
                SelectIndex((_selectedIndex + 1) % _options.Count);
            }
        }

        /// <summary>
        /// 处理交互输入（确认当前选项）。
        /// </summary>
        private void OnInteractInput(InputInteractEvent evt)
        {
            OnButtonClick(new BoardActionButtonClickEvent { Index = _selectedIndex });
        }

        /// <summary>
        /// 鼠标悬停回调：切换为鼠标选择源。
        /// </summary>
        private void OnButtonHover(BoardActionButtonHoverEvent evt)
        {
            int index = evt.Index;
            _selectionSource = SelectionSource.Mouse;
            SelectIndex(index);
        }

        /// <summary>
        /// 鼠标离开回调：仅在鼠标选择源时清理高亮。
        /// </summary>
        private void OnButtonExit(BoardActionButtonExitEvent evt)
        {
            if (_selectionSource != SelectionSource.Mouse) return;
            ClearSelection();
        }

        /// <summary>
        /// 点击处理：根据按钮类型触发对应事件。
        /// </summary>
        private void OnButtonClick(BoardActionButtonClickEvent evt)
        {
            int index = evt.Index;
            if (index < 0 || index >= _options.Count) return;
            BoardActionOptionData option = _options[index];
            switch (option.Id)
            {
                case BoardActionId.RollDice:
                    EventBus.Raise(new BoardRollDiceRequestedEvent());
                    Hide();
                    break;
                case BoardActionId.Item:
                    EventBus.Raise(new OpenInventoryEvent());
                    Hide();
                    break;
                case BoardActionId.Camp:
                    if (campingLocationId == null)
                    {
                        Debug.LogWarning("[BoardActionMenuView] Missing campingLocationId.");
                        break;
                    }
                    if (BoardGameManager.Instance != null)
                    {
                        BoardGameManager.Instance.ChangeState(new CampingState(campingLocationId));
                    }
                    break;
            }
        }

        /// <summary>
        /// 选中某一按钮并更新动画缩放。
        /// </summary>
        private void SelectIndex(int index, bool instant = false)
        {
            if (_options.Count == 0) return;
            _selectedIndex = Mathf.Clamp(index, 0, _options.Count - 1);

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null || !_buttons[i].gameObject.activeSelf) continue;
                bool isSelected = i == _selectedIndex;
                _buttons[i].SetSelected(isSelected, isSelected ? selectedScale : normalScale, instant ? 0f : selectTweenDuration);
            }
        }

        /// <summary>
        /// 清理选中状态，恢复所有按钮为普通缩放。
        /// </summary>
        private void ClearSelection()
        {
            _selectedIndex = -1;
            _selectionSource = SelectionSource.None;

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].SetSelected(false, normalScale, selectTweenDuration);
            }
        }

        /// <summary>
        /// 播放显示动画。
        /// </summary>
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
                if (_buttons[i] == null || !_buttons[i].gameObject.activeSelf) continue;
                Transform t = _buttons[i].transform;
                t.localScale = Vector3.zero;
                _showSequence.Append(t.DOScale(normalScale, showDuration).SetEase(showEase));
                if (i < _options.Count - 1) _showSequence.AppendInterval(showStagger);
            }
        }

        /// <summary>
        /// 播放隐藏动画。
        /// </summary>
        private void PlayHideAnimation()
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();

            _hideSequence = DOTween.Sequence();
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null || !_buttons[i].gameObject.activeSelf) continue;
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

        /// <summary>
        /// 订阅输入事件（EventBus）。
        /// </summary>
        private void SubscribeInput()
        {
            if (_inputSubscribed) return;
            EventBus.Subscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Subscribe<InputInteractEvent>(OnInteractInput);
            EventBus.Subscribe<BoardActionButtonHoverEvent>(OnButtonHover);
            EventBus.Subscribe<BoardActionButtonClickEvent>(OnButtonClick);
            EventBus.Subscribe<BoardActionButtonExitEvent>(OnButtonExit);
            _inputSubscribed = true;
        }

        /// <summary>
        /// 退订输入事件。
        /// </summary>
        private void UnsubscribeInput()
        {
            if (!_inputSubscribed) return;
            EventBus.Unsubscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Unsubscribe<InputInteractEvent>(OnInteractInput);
            EventBus.Unsubscribe<BoardActionButtonHoverEvent>(OnButtonHover);
            EventBus.Unsubscribe<BoardActionButtonClickEvent>(OnButtonClick);
            EventBus.Unsubscribe<BoardActionButtonExitEvent>(OnButtonExit);
            _inputSubscribed = false;
        }

        /// <summary>
        /// 从已有子节点构建按钮池（编辑器预摆按钮时使用）。
        /// </summary>
        private void EnsurePoolFromChildren()
        {
            if (_buttons.Count > 0 || binder.ButtonContainer == null) return;
            for (int i = 0; i < binder.ButtonContainer.childCount; i++)
            {
                var button = binder.ButtonContainer.GetChild(i).GetComponent<BoardActionButton>();
                if (button == null) continue;
                button.gameObject.SetActive(false);
                _buttons.Add(button);
            }
        }

        /// <summary>
        /// 获取或创建指定索引的按钮。
        /// </summary>
        private BoardActionButton GetOrCreateButton(int index)
        {
            if (index < _buttons.Count && _buttons[index] != null)
            {
                return _buttons[index];
            }

            if (binder.ButtonPrefab == null || binder.ButtonContainer == null) return null;
            BoardActionButton button = Instantiate(binder.ButtonPrefab);
            button.transform.SetParent(binder.ButtonContainer, false);
            if (index < _buttons.Count)
            {
                _buttons[index] = button;
            }
            else
            {
                _buttons.Add(button);
            }
            return button;
        }
    }
}
