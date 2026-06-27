using IndieGame.Core.Utilities;
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
    /// 菜单以玩家头顶为锚点呈弧形展开；仅支持键盘交互（A/D 或方向键左右切换选项，E 确认），鼠标点击/悬停已禁用。
    /// </summary>
    public class BoardActionMenuView : MonoBehaviour
    {
        [Header("Binder")]
        // 绑定器：集中保存 UI 引用
        [SerializeField] private BoardActionMenuBinder binder;

        [Header("Dependencies")]
        // 追踪目标（菜单会围绕该目标投影位置展示，通常为玩家）
        public Transform target;
        [Tooltip("露营场景对应的 LocationID（在 Inspector 中配置）")]
        [SerializeField] private LocationID campingLocationId;

        [Header("Layout")]
        // 锚点相对目标的世界坐标偏移（用于把菜单抬高到角色头顶位置，需在 Inspector 中按角色模型实际高度微调）
        public Vector3 targetWorldOffset = new Vector3(0f, 1.8f, 0f);
        // 按钮半径（围绕锚点的距离）
        public float radius = 120f;
        // 弧形角度范围（以锚点正上方为中心，向左右展开）
        public float arcAngle = 140f;
        // 屏幕偏移（在弧形布局基础上的额外微调，通常保持 0）
        public Vector2 offset = Vector2.zero;

        [Header("Selection")]
        // 选中缩放倍数
        public float selectedScale = 1.15f;
        // 普通缩放
        public float normalScale = 1f;
        // 选中切换动画时长
        public float selectTweenDuration = 0.12f;
        // 按住方向键不放时的自动连发间隔（单次按键始终立即响应，不受此值影响）
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
        // 上一帧的水平输入值，用于检测“新按键”边沿，使单次按键不受连发节流影响
        private float _lastInputX = 0f;
        private Sequence _showSequence;
        private Sequence _hideSequence;
        private RectTransform _selfRect;
        private RectTransform _canvasRect;
        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;
        private bool _inputSubscribed = false;
        private Camera _mainCam;

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[BoardActionMenuView] Missing binder reference.");
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

        /// <summary>
        /// 编辑器下调整 Layout 参数（radius/arcAngle/offset 等）时立即重新布局，
        /// 方便在 Play 模式中拖动 Inspector 数值实时看到效果。
        /// LayoutButtons 只在 Show() 时被调用一次，单靠运行时帧更新无法反映 Inspector 改动，
        /// 因此通过 OnValidate（仅在编辑器内、Inspector 数值变化时触发）手动补一次布局刷新。
        /// </summary>
        private void OnValidate()
        {
            if (_buttons == null || _buttons.Count == 0) return;
            LayoutButtons();
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
            Vector3 screenPos = _mainCam.WorldToScreenPoint(target.position + targetWorldOffset);
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
            EventBus.Raise(new BoardActionMenuShownEvent { Target = target });
        }

        /// <summary>
        /// 隐藏菜单。
        /// </summary>
        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false; // 立即标记，防止动画期间 Show() 被守卫拦截
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
                DebugTools.LogError("[BoardActionMenuView] ButtonPrefab must be a prefab asset, not a scene object.");
                return;
            }
            bool parentValid = binder.ButtonContainer.gameObject.scene.IsValid();
            if (!parentValid)
            {
                DebugTools.LogWarning("[BoardActionMenuView] ButtonContainer is not a scene object, skipping button rebuild.");
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
            _lastInputX = 0f;
        }

        /// <summary>
        /// 计算并设置按钮在弧形布局中的位置：
        /// 以锚点正上方（90°）为中心向左右展开，索引 0 对应最左侧按钮，
        /// 索引递增时按钮位置从左向右排列，方便与“A 左移 / D 右移”的选择逻辑直接对应。
        /// </summary>
        private void LayoutButtons()
        {
            int activeCount = _options.Count;
            if (activeCount == 0) return;

            float startAngle = 90f + arcAngle * 0.5f;
            float step = activeCount > 1 ? -arcAngle / (activeCount - 1) : 0f;

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
        /// 处理移动输入（左右切换选项，对应弧形菜单从左到右的按钮排列）：
        /// A / 左方向键 -> 选中左侧选项；D / 右方向键 -> 选中右侧选项。
        /// 复用现有的 Move 输入轴（与角色移动共用同一组按键），无需额外绑定。
        ///
        /// 节流逻辑只用于“按住不放”时的自动连发，新的单次按键（方向从中立或反方向切换过来）
        /// 一律立即响应，避免快速连续按键时因节流而出现选择顿挫、漏按的问题。
        /// </summary>
        private void OnMoveInput(InputMoveEvent evt)
        {
            float x = evt.Value.x;
            if (_options.Count == 0) { _lastInputX = x; return; }

            bool wasActive = Mathf.Abs(_lastInputX) > 0.5f;
            bool isActive = Mathf.Abs(x) > 0.5f;
            bool isNewPress = isActive && (!wasActive || Mathf.Sign(x) != Mathf.Sign(_lastInputX));

            if (isNewPress)
            {
                // 新按键：立即响应，并重新计时下一次自动连发的等待时间
                MoveSelection(x > 0f ? 1 : -1);
                _nextInputTime = Time.time + inputRepeatDelay;
            }
            else if (isActive && Time.time >= _nextInputTime)
            {
                // 持续按住：按固定间隔自动连发（用于手柄摇杆等连续输入）
                MoveSelection(x > 0f ? 1 : -1);
                _nextInputTime = Time.time + inputRepeatDelay;
            }

            _lastInputX = x;
        }

        /// <summary>
        /// 按方向切换当前选中项（+1 向右，-1 向左）。
        /// </summary>
        private void MoveSelection(int direction)
        {
            SelectIndex((_selectedIndex + direction + _options.Count) % _options.Count);
        }

        /// <summary>
        /// 处理交互输入（E 键 / Interact 输入 -> 确认当前选中的选项）。
        /// </summary>
        private void OnInteractInput(InputInteractEvent evt)
        {
            OnButtonClick(new BoardActionButtonClickEvent { Index = _selectedIndex });
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
                case BoardActionId.Treasure:
                    Hide(); // 先隐藏操作菜单，再通知，确保 _isVisible=false 时恢复逻辑可正常触发 Show
                    EventBus.Raise(new BoardTreasureMenuRequestedEvent());
                    break;
                case BoardActionId.Camp:
                    if (campingLocationId == null)
                    {
                        DebugTools.LogWarning("[BoardActionMenuView] Missing campingLocationId.");
                        break;
                    }
                    if (BoardGameManager.Instance != null)
                    {
                        BoardGameManager.Instance.ChangeState(new CampingState(campingLocationId));
                    }
                    break;
                case BoardActionId.Map:
                    // TODO: 地图功能尚未实现，先用日志占位点击效果
                    DebugTools.Log("<color=cyan>[操作菜单] 点击了【地图】按钮（功能待实现）。</color>");
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
        /// 播放显示动画：
        /// 仅支持键盘操作，因此显示期间始终关闭 CanvasGroup 的鼠标交互（blocksRaycasts/interactable）。
        /// 当前选中的按钮（默认第一个）放大到 selectedScale，其余按钮为 normalScale。
        /// </summary>
        private void PlayShowAnimation()
        {
            _showSequence?.Kill();
            _hideSequence?.Kill();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            _showSequence = DOTween.Sequence();
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null || !_buttons[i].gameObject.activeSelf) continue;
                Transform t = _buttons[i].transform;
                float targetScale = i == _selectedIndex ? selectedScale : normalScale;
                t.localScale = Vector3.zero;
                _showSequence.Append(t.DOScale(targetScale, showDuration).SetEase(showEase));
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
