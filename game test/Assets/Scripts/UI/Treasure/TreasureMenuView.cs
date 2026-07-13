using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Treasure;

namespace IndieGame.UI.Treasure
{
    /// <summary>
    /// 通用菜单条目：供非宝具场景（如斗篷传送目标）复用此菜单时使用。
    /// Id 将作为 TreasureItemSelectedEvent.TreasureId 发布，由调用方解析含义。
    /// </summary>
    public struct SimpleMenuItem
    {
        public string Id;
        public string DisplayText;
    }

    /// <summary>
    /// 宝具菜单视图：自管理 UI（仿 ShopUIController 模式）。
    /// 订阅 BoardTreasureMenuRequestedEvent 后自动展示宝具列表，
    /// 玩家确认后发布 TreasureItemSelectedEvent，取消后发布 TreasureMenuCancelledEvent。
    /// 也可通过 ShowSimple() 以纯文本列表形式复用此菜单（如斗篷传送目标选择）。
    /// </summary>
    public class TreasureMenuView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private TreasureMenuBinder binder;

        [Header("Animation")]
        [Tooltip("显示动画时长")]
        public float showDuration = 0.2f;
        [Tooltip("隐藏动画时长")]
        public float hideDuration = 0.15f;
        [Tooltip("显示缓动曲线")]
        public Ease showEase = Ease.OutBack;
        [Tooltip("隐藏缓动曲线")]
        public Ease hideEase = Ease.InBack;

        [Header("Input")]
        [Tooltip("方向键连发间隔（秒）")]
        public float inputRepeatDelay = 0.2f;
        [Tooltip("用于监听 ESC/手柄 Cancel 的输入读取器（与其他面板共用同一份 GameInputReader 资产）")]
        [SerializeField] private GameInputReader inputReader;

        // --- 运行时状态 ---
        private readonly List<TreasureSlotUI> _slots   = new List<TreasureSlotUI>();
        private readonly List<TreasureSO>     _options = new List<TreasureSO>();
        // 简单模式（ShowSimple）的条目列表；两种模式互斥，_simpleMode 标记当前生效的
        private readonly List<SimpleMenuItem> _simpleOptions = new List<SimpleMenuItem>();
        private bool _simpleMode;
        // 用于跳过菜单显示后的第一次 InputInteractCanceledEvent：
        // 玩家在操作菜单/上一级菜单按确认键触发本菜单显示时，确认键往往还未松开，
        // Show()/ShowSimple() 会立即重新订阅输入，此时松开确认键会被误判为"取消"，
        // 导致菜单刚打开就被自动关闭（表现为"必须一直按住确认键菜单才不消失"）。
        // 设置此标志让第一次取消事件静默，与 WingTreasureState 用 UICancelEvent 的原因相同。
        private bool _suppressNextCancel;
        private int _selectedIndex = -1;
        private float _nextInputTime;
        private bool _isVisible;
        private bool _inputSubscribed;
        private CanvasGroup _canvasGroup;
        private RectTransform _rootRect;
        private Sequence _showSeq;
        private Sequence _hideSeq;
        // ESC/手柄 Cancel 关闭绑定：与"松开确认键取消"并存的另一条关闭路径，互不影响
        private EscCloseBinding _escBinding;

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[TreasureMenuView] 缺少 Binder 引用。");
                return;
            }
            _canvasGroup = binder.CanvasGroup;
            _rootRect    = binder.RootRect;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable   = false;
            }

            _escBinding = new EscCloseBinding(inputReader, () => _isVisible, HandleEscCancel);
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            _showSeq?.Kill();
            _hideSeq?.Kill();
        }

        // ── 公开接口 ──────────────────────────────────────────────────────

        // 当前列表总条目数（统一两种模式）
        private int OptionCount => _simpleMode ? _simpleOptions.Count : _options.Count;

        /// <summary>
        /// 展示宝具菜单（幂等：已显示时忽略）。
        /// </summary>
        public void Show(IReadOnlyList<TreasureSO> treasures)
        {
            if (_isVisible) return;
            _simpleMode = false;
            // 本菜单通常由操作菜单“宝具”按钮的确认键直接触发显示，该键此时大概率还未松开，
            // 需要静默第一次取消事件，避免菜单刚打开就被误判为“取消”而自动关闭
            _suppressNextCancel = true;
            RefreshSlots(treasures);
            SelectIndex(0, instant: true);
            PlayShowAnimation();
            _isVisible = true;
            SubscribeInput();
        }

        /// <summary>
        /// 以纯文本列表展示菜单（复用宝具菜单 UI，适用于斗篷等传送目标选择）。
        /// 确认时仍发布 TreasureItemSelectedEvent，TreasureId 为对应条目的 Id 字段。
        /// </summary>
        public void ShowSimple(IReadOnlyList<SimpleMenuItem> items)
        {
            if (_isVisible) return;
            _simpleMode = true;
            _suppressNextCancel = true;
            RefreshSlotsSimple(items);
            SelectIndex(0, instant: true);
            PlayShowAnimation();
            _isVisible = true;
            SubscribeInput();
        }

        /// <summary>
        /// 隐藏宝具菜单（幂等）。
        /// </summary>
        public void Hide()
        {
            if (!_isVisible) return;
            _isVisible = false; // 立即标记，防止动画期间 Show() 被 _isVisible 守卫拦截
            UnsubscribeInput();
            PlayHideAnimation();
        }

        // ── 输入处理 ──────────────────────────────────────────────────────

        private void OnMoveInput(InputMoveEvent evt)
        {
            int count = OptionCount;
            if (count == 0 || Time.time < _nextInputTime) return;

            if (evt.Value.y > 0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                SelectIndex((_selectedIndex - 1 + count) % count);
            }
            else if (evt.Value.y < -0.5f)
            {
                _nextInputTime = Time.time + inputRepeatDelay;
                SelectIndex((_selectedIndex + 1) % count);
            }
        }

        private void OnInteractInput(InputInteractEvent evt)
        {
            string id;
            if (_simpleMode)
            {
                if (_selectedIndex < 0 || _selectedIndex >= _simpleOptions.Count) return;
                id = _simpleOptions[_selectedIndex].Id;
            }
            else
            {
                if (_selectedIndex < 0 || _selectedIndex >= _options.Count) return;
                id = _options[_selectedIndex].TreasureId;
            }
            Hide();
            EventBus.Raise(new TreasureItemSelectedEvent { TreasureId = id });
        }

        private void OnCancelInput(InputInteractCanceledEvent evt)
        {
            if (_suppressNextCancel) { _suppressNextCancel = false; return; }
            Hide();
            EventBus.Raise(new TreasureMenuCancelledEvent());
        }

        /// <summary>
        /// ESC/手柄 Cancel 关闭：与确认键松开取消是两条独立路径，
        /// ESC 是专用按键，不存在"打开时按键还未松开"的问题，无需 _suppressNextCancel。
        /// </summary>
        private void HandleEscCancel()
        {
            Hide();
            EventBus.Raise(new TreasureMenuCancelledEvent());
        }

        // ── 内部逻辑 ──────────────────────────────────────────────────────

        private void RefreshSlots(IReadOnlyList<TreasureSO> treasures)
        {
            _options.Clear();

            if (binder == null || binder.SlotPrefab == null || binder.SlotContainer == null) return;

            // 隐藏旧 Slot
            foreach (var slot in _slots)
                if (slot != null) slot.gameObject.SetActive(false);

            // 复用或新建 Slot
            for (int i = 0; i < treasures.Count; i++)
            {
                TreasureSlotUI slot = i < _slots.Count ? _slots[i] : CreateSlot();
                if (slot == null) break;

                slot.Setup(treasures[i]);
                slot.gameObject.SetActive(true);
                _options.Add(treasures[i]);
            }

            _selectedIndex = -1;
        }

        private TreasureSlotUI CreateSlot()
        {
            TreasureSlotUI slot = Instantiate(binder.SlotPrefab, binder.SlotContainer, false);
            _slots.Add(slot);
            return slot;
        }

        private void SelectIndex(int index, bool instant = false)
        {
            int count = OptionCount;
            if (count == 0) return;
            _selectedIndex = Mathf.Clamp(index, 0, count - 1);

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null || !_slots[i].gameObject.activeSelf) continue;
                _slots[i].SetHighlighted(i == _selectedIndex);
            }
        }

        private void RefreshSlotsSimple(IReadOnlyList<SimpleMenuItem> items)
        {
            _simpleOptions.Clear();

            if (binder == null || binder.SlotPrefab == null || binder.SlotContainer == null) return;

            foreach (var slot in _slots)
                if (slot != null) slot.gameObject.SetActive(false);

            for (int i = 0; i < items.Count; i++)
            {
                TreasureSlotUI slot = i < _slots.Count ? _slots[i] : CreateSlot();
                if (slot == null) break;

                slot.SetupSimple(items[i].DisplayText);
                slot.gameObject.SetActive(true);
                _simpleOptions.Add(items[i]);
            }

            _selectedIndex = -1;
        }

        // ── 动画 ──────────────────────────────────────────────────────────

        private void PlayShowAnimation()
        {
            _showSeq?.Kill();
            _hideSeq?.Kill();

            if (_canvasGroup == null) return;
            _canvasGroup.alpha          = 0f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable   = true;

            if (_rootRect != null) _rootRect.localScale = Vector3.one * 0.85f;

            _showSeq = DOTween.Sequence();
            if (_rootRect != null)
                _showSeq.Join(_rootRect.DOScale(1f, showDuration).SetEase(showEase));
            _showSeq.Join(_canvasGroup.DOFade(1f, showDuration));
        }

        private void PlayHideAnimation()
        {
            _showSeq?.Kill();
            _hideSeq?.Kill();

            if (_canvasGroup == null)
            {
                _isVisible = false;
                return;
            }

            _hideSeq = DOTween.Sequence();
            if (_rootRect != null)
                _hideSeq.Join(_rootRect.DOScale(0.85f, hideDuration).SetEase(hideEase));
            _hideSeq.Join(_canvasGroup.DOFade(0f, hideDuration));
            _hideSeq.OnComplete(() =>
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.blocksRaycasts = false;
                    _canvasGroup.interactable   = false;
                }
                _isVisible = false;
            });
        }

        // ── 输入订阅管理 ──────────────────────────────────────────────────

        private void SubscribeInput()
        {
            if (_inputSubscribed) return;
            EventBus.Subscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Subscribe<InputInteractEvent>(OnInteractInput);
            EventBus.Subscribe<InputInteractCanceledEvent>(OnCancelInput);
            _escBinding?.Subscribe();
            _inputSubscribed = true;
        }

        private void UnsubscribeInput()
        {
            if (!_inputSubscribed) return;
            EventBus.Unsubscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Unsubscribe<InputInteractEvent>(OnInteractInput);
            EventBus.Unsubscribe<InputInteractCanceledEvent>(OnCancelInput);
            _escBinding?.Unsubscribe();
            _inputSubscribed = false;
        }
    }
}
