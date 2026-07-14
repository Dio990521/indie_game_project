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
    /// 菜单以玩家为锚点，按钮分为左右两侧圆弧展开（左侧朝左侧鼓出，右侧朝右侧鼓出）。
    /// 两侧按钮视为一个 3 行 x 2 列的网格：水平方向（A/D 或左右方向键）在左右两侧之间切换，
    /// 垂直方向（W/S 或上下方向键）在当前侧内部切换行；E 确认。鼠标点击/悬停已禁用。
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
        // 按钮半径（围绕锚点的距离，左右两侧共用同一半径）
        public float radius = 120f;
        // 单侧弧形角度范围（以该侧圆弧中心——左侧 180°/右侧 0°——为中心，纵向向上下展开）
        public float arcAngle = 140f;
        // 屏幕偏移（在弧形布局基础上的额外微调，左右两侧共用，通常保持 0）
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
        // 左/右两侧按钮的“扁平索引”列表（索引指向 _options/_buttons），按从上到下的行顺序排列，
        // 用于圆弧布局与 3x2 网格式方向键选择
        private readonly List<int> _leftFlatIndices = new List<int>();
        private readonly List<int> _rightFlatIndices = new List<int>();
        private int _selectedIndex = -1;
        // 当前选中所在的侧（0=左侧，1=右侧）与行号（0 表示该侧最上方一行）
        private int _selectedSide = 0;
        private int _selectedRow = 0;
        private float _nextInputTime = 0f;
        private float _nextInputTimeY = 0f;
        // 上一帧的水平/垂直输入值，用于检测“新按键”边沿，使单次按键不受连发节流影响
        private float _lastInputX = 0f;
        private float _lastInputY = 0f;
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
            // L1 修复：默认跟随玩家，但玩家可能尚未创建（初始化顺序变化 / 标题场景）。
            // 这里只做尝试绑定，失败时留空，由 LateUpdate 的懒绑定兜底。
            TryResolveTarget();
        }

        /// <summary>
        /// 懒绑定跟随目标：玩家对象由 GameManager 运行时生成，
        /// 本 UI 的 Start 可能早于玩家创建，因此提供可重入的解析入口。
        /// </summary>
        private bool TryResolveTarget()
        {
            if (target != null) return true;
            if (GameManager.Instance == null || GameManager.Instance.CurrentPlayer == null) return false;
            target = GameManager.Instance.CurrentPlayer.transform;
            return true;
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
            // L1 修复：目标为空时尝试懒绑定（玩家可能在本 UI Start 之后才创建）
            if (target == null && !TryResolveTarget()) return;
            if (_selfRect == null || _canvasRect == null) return;
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
            // 优先选中左侧第一行；若左侧为空（理论上不应发生）则回退到右侧第一行
            SelectSideRow(_leftFlatIndices.Count > 0 ? 0 : 1, 0, instant: true);
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

            // 按左右两侧分组，保持各侧内部原始顺序（用于圆弧布局与方向键的行号映射）
            _leftFlatIndices.Clear();
            _rightFlatIndices.Clear();
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Side == BoardActionSide.Left) _leftFlatIndices.Add(i);
                else _rightFlatIndices.Add(i);
            }

            _selectedIndex = -1;
            _lastInputX = 0f;
            _lastInputY = 0f;
        }

        /// <summary>
        /// 计算并设置按钮在圆弧布局中的位置：
        /// 左侧按钮以玩家左侧（180°）为中心呈弧形纵向展开，右侧按钮以玩家右侧（0°）为中心呈弧形纵向展开；
        /// 每侧内部 row 0 始终在最上方，向下依次排列，与方向键的“3x2 网格”选择逻辑一一对应。
        /// </summary>
        private void LayoutButtons()
        {
            LayoutSideArc(_leftFlatIndices, 180f, flipStep: true);
            LayoutSideArc(_rightFlatIndices, 0f, flipStep: false);
        }

        /// <summary>
        /// 将某一侧的按钮沿圆弧纵向排列（row 0 在最上方，中间行朝该侧鼓出最多）。
        /// </summary>
        /// <param name="flatIndices">该侧按钮在 _buttons 中的索引列表，按行从上到下排列</param>
        /// <param name="centerAngle">该侧圆弧中心角度：左侧 180°，右侧 0°</param>
        /// <param name="flipStep">左右两侧的角度增长方向相反，用于保证 row 0 始终位于最上方</param>
        private void LayoutSideArc(List<int> flatIndices, float centerAngle, bool flipStep)
        {
            int count = flatIndices.Count;
            if (count == 0) return;

            float half = arcAngle * 0.5f * (flipStep ? -1f : 1f);
            float step = (count > 1 ? arcAngle / (count - 1) : 0f) * (flipStep ? 1f : -1f);
            float startAngle = centerAngle + half;

            for (int row = 0; row < count; row++)
            {
                BoardActionButton button = _buttons[flatIndices[row]];
                if (button == null) continue;
                float angle = startAngle + step * row;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius) + offset;

                RectTransform rt = button.GetComponent<RectTransform>();
                rt.anchoredPosition = pos;
            }
        }

        /// <summary>
        /// 处理移动输入：将左右两侧按钮视为一个 3 行 x 2 列的网格。
        /// 水平方向（A/D 或左右方向键）在“左侧栏 / 右侧栏”之间切换；
        /// 垂直方向（W/S 或上下方向键）在当前所在侧内部按行循环切换。
        /// 复用现有的 Move 输入轴（与角色移动共用同一组按键），无需额外绑定。
        ///
        /// 两个轴各自独立做“新按键立即响应 + 按住连发”的节流判定，互不干扰；
        /// 节流逻辑只用于“按住不放”时的自动连发，新的单次按键（方向从中立或反方向切换过来）
        /// 一律立即响应，避免快速连续按键时因节流而出现选择顿挫、漏按的问题。
        /// </summary>
        private void OnMoveInput(InputMoveEvent evt)
        {
            Vector2 v = evt.Value;
            if (_options.Count == 0) { _lastInputX = v.x; _lastInputY = v.y; return; }

            ProcessAxis(v.x, ref _lastInputX, ref _nextInputTime, MoveSide);
            // 屏幕纵坐标向上为正，而 row 0 在最上方，因此按“上”时需要让 row 减小，符号取反
            ProcessAxis(v.y, ref _lastInputY, ref _nextInputTimeY, dir => MoveRow(-dir));
        }

        /// <summary>
        /// 单个输入轴的“新按键立即响应 + 按住连发”节流通用逻辑。
        /// </summary>
        private void ProcessAxis(float value, ref float lastValue, ref float nextTime, Action<int> onStep)
        {
            bool wasActive = Mathf.Abs(lastValue) > 0.5f;
            bool isActive = Mathf.Abs(value) > 0.5f;
            bool isNewPress = isActive && (!wasActive || Mathf.Sign(value) != Mathf.Sign(lastValue));

            if (isNewPress)
            {
                onStep(value > 0f ? 1 : -1);
                nextTime = Time.time + inputRepeatDelay;
            }
            else if (isActive && Time.time >= nextTime)
            {
                onStep(value > 0f ? 1 : -1);
                nextTime = Time.time + inputRepeatDelay;
            }

            lastValue = value;
        }

        /// <summary>
        /// 左右切换当前选中所在的侧（+1 右，-1 左）；已在最左/最右侧时不循环。
        /// 若目标侧的行数少于当前行号，则夹取到该侧最后一行。
        /// </summary>
        private void MoveSide(int direction)
        {
            int targetSide = direction > 0 ? 1 : 0;
            if (targetSide == _selectedSide) return;

            List<int> target = targetSide == 0 ? _leftFlatIndices : _rightFlatIndices;
            if (target.Count == 0) return;

            int row = Mathf.Min(_selectedRow, target.Count - 1);
            SelectSideRow(targetSide, row);
        }

        /// <summary>
        /// 在当前侧内部上下循环切换选中行（+1 向下，-1 向上）。
        /// </summary>
        private void MoveRow(int direction)
        {
            List<int> current = _selectedSide == 0 ? _leftFlatIndices : _rightFlatIndices;
            if (current.Count == 0) return;

            int row = (_selectedRow + direction + current.Count) % current.Count;
            SelectSideRow(_selectedSide, row);
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
                case BoardActionId.Equip:
                    EventBus.Raise(new OpenEquipmentUIEvent());
                    Hide();
                    break;
            }
        }

        /// <summary>
        /// 选中指定侧、指定行的按钮，并更新所有按钮的高亮缩放。
        /// </summary>
        private void SelectSideRow(int side, int row, bool instant = false)
        {
            List<int> list = side == 0 ? _leftFlatIndices : _rightFlatIndices;
            if (list.Count == 0) return;
            row = Mathf.Clamp(row, 0, list.Count - 1);

            _selectedSide = side;
            _selectedRow = row;
            _selectedIndex = list[row];

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
