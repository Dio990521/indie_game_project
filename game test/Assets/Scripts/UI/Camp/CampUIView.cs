using IndieGame.Core.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Gameplay.Camp;
using IndieGame.Core;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 视图（View）：
    /// <para>
    /// 仅负责"如何显示"，不负责"业务编排"。具体职责包括：
    /// - 动态生成按钮并维护索引映射；
    /// - 淡入/淡出动画；
    /// - 纯键盘方向键选中 + 交互键确认，转发到对应的业务事件（如 Sleep 通过 EventBus 转发为 CampSleepRequestedEvent）。
    /// </para>
    /// <para>
    /// 菜单只支持键盘操作，不响应鼠标点击（按钮不实现任何 Pointer 事件接口）。
    /// </para>
    /// <para>
    /// 所有跨系统编排（恢复行动点、推进日期、自动存档、返回棋盘等）已迁移到 <see cref="CampUIController"/>。
    /// </para>
    /// </summary>
    public class CampUIView : View
    {
        [Header("Binder")]
        // UI 绑定器（负责动态菜单生成）
        [SerializeField] private CampUIBinder binder;

        [Header("Config")]
        // 默认解锁动作（可在 Inspector 中配置）
        [Tooltip("默认解锁的动作列表（可在 Inspector 配置）")]
        [SerializeField] private List<CampActionSO> defaultActions = new List<CampActionSO>();

        [Header("Selection")]
        // 选中缩放倍数
        [SerializeField] private float selectedScale = 1.15f;
        // 普通缩放
        [SerializeField] private float normalScale = 1f;
        // 选中切换动画时长
        [SerializeField] private float selectTweenDuration = 0.12f;
        // 按住方向键不放时的自动连发间隔（单次按键始终立即响应，不受此值影响）
        [SerializeField] private float inputRepeatDelay = 0.2f;

        // CanvasGroup 控制淡入淡出
        private CanvasGroup _canvasGroup;
        // 当前解锁动作缓存（用于索引映射）
        private readonly List<CampActionSO> _currentActions = new List<CampActionSO>();
        // 当前已生成的按钮实例（用于选中高亮）
        private readonly List<CampActionButton> _buttons = new List<CampActionButton>();

        // --- 键盘导航状态 ---
        private int _selectedIndex = -1;
        private float _nextInputTime = 0f;
        private float _lastInputY = 0f;
        private bool _inputSubscribed = false;

        private void Awake()
        {
            if (binder == null)
            {
                DebugTools.LogError("[CampUIView] Missing binder reference.");
                return;
            }
            // 初始化 CanvasGroup
            _canvasGroup = binder.CanvasGroup != null ? binder.CanvasGroup : GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private void OnDisable()
        {
            // 兜底：菜单被禁用时确保输入订阅被清理，避免残留订阅导致隐藏状态下仍响应按键。
            UnsubscribeInput();
        }

        /// <summary>
        /// 显示露营 UI，并淡入。
        /// </summary>
        public override void Show()
        {
            Show(defaultActions);
        }

        /// <summary>
        /// 显示并初始化菜单。
        /// </summary>
        public void Show(List<CampActionSO> unlockedActions)
        {
            // 初始化按钮列表
            InitializeMenu(unlockedActions);

            // 默认选中第一项
            SelectIndex(_currentActions.Count > 0 ? 0 : -1, instant: true);

            // 触发淡入动画
            StopAllCoroutines();
            StartCoroutine(FadeInRoutine());

            SubscribeInput();
        }

        public override void Hide()
        {
            UnsubscribeInput();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private IEnumerator FadeInRoutine()
        {
            // 菜单只支持键盘操作，不需要 Raycast 阻挡/交互，故不开启 blocksRaycasts/interactable。
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.25f);
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>
        /// 动态初始化菜单：
        /// 传入已解锁动作列表，自动生成按钮。
        /// </summary>
        private void InitializeMenu(List<CampActionSO> unlockedActions)
        {
            if (binder == null || binder.MenuContainer == null || binder.ButtonPrefab == null) return;
            _currentActions.Clear();
            _buttons.Clear();

            // 清空旧按钮
            for (int i = binder.MenuContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(binder.MenuContainer.GetChild(i).gameObject);
            }

            if (unlockedActions == null) return;

            for (int i = 0; i < unlockedActions.Count; i++)
            {
                CampActionSO action = unlockedActions[i];
                if (action == null) continue;
                _currentActions.Add(action);

                // 实例化按钮并挂到容器
                CampActionButton button = Instantiate(binder.ButtonPrefab, binder.MenuContainer);
                button.Setup(action.DisplayName, action.Icon);
                _buttons.Add(button);
            }
        }

        /// <summary>
        /// 处理方向键输入：菜单为单列纵向排列，只响应垂直轴（W/S 或上下方向键）。
        /// 新按键立即响应，按住不放则按 inputRepeatDelay 节流连发。
        /// </summary>
        private void OnMoveInput(InputMoveEvent evt)
        {
            float value = evt.Value.y;
            if (_currentActions.Count == 0) { _lastInputY = value; return; }

            bool wasActive = Mathf.Abs(_lastInputY) > 0.5f;
            bool isActive = Mathf.Abs(value) > 0.5f;
            bool isNewPress = isActive && (!wasActive || Mathf.Sign(value) != Mathf.Sign(_lastInputY));

            if (isNewPress)
            {
                // 屏幕纵坐标向上为正，按“上”时选中项应向列表顶部移动（索引减小）。
                MoveSelection(value > 0f ? -1 : 1);
                _nextInputTime = Time.time + inputRepeatDelay;
            }
            else if (isActive && Time.time >= _nextInputTime)
            {
                MoveSelection(value > 0f ? -1 : 1);
                _nextInputTime = Time.time + inputRepeatDelay;
            }

            _lastInputY = value;
        }

        /// <summary>
        /// 交互键确认：等价于点击当前选中的按钮。
        /// </summary>
        private void OnInteractInput(InputInteractEvent evt)
        {
            ConfirmSelection(_selectedIndex);
        }

        /// <summary>
        /// 按方向在当前列表内循环移动选中索引。
        /// </summary>
        private void MoveSelection(int direction)
        {
            int count = _currentActions.Count;
            if (count == 0) return;
            int index = _selectedIndex < 0 ? 0 : (_selectedIndex + direction + count) % count;
            SelectIndex(index);
        }

        /// <summary>
        /// 选中指定索引的按钮，并更新所有按钮的高亮缩放。
        /// </summary>
        private void SelectIndex(int index, bool instant = false)
        {
            _selectedIndex = index;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null) continue;
                bool isSelected = i == _selectedIndex;
                _buttons[i].SetSelected(isSelected, isSelected ? selectedScale : normalScale, instant ? 0f : selectTweenDuration);
            }
        }

        /// <summary>
        /// 订阅方向键 / 交互键输入（仅在菜单显示期间订阅）。
        /// </summary>
        private void SubscribeInput()
        {
            if (_inputSubscribed) return;
            EventBus.Subscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Subscribe<InputInteractEvent>(OnInteractInput);
            _inputSubscribed = true;
        }

        /// <summary>
        /// 退订方向键 / 交互键输入。
        /// </summary>
        private void UnsubscribeInput()
        {
            if (!_inputSubscribed) return;
            EventBus.Unsubscribe<InputMoveEvent>(OnMoveInput);
            EventBus.Unsubscribe<InputInteractEvent>(OnInteractInput);
            _inputSubscribed = false;
        }

        /// <summary>
        /// 确认选中项：按索引映射回动作数据，按 ActionID 转发为对应业务事件，让 Controller / 系统处理。
        /// View 自身不再调用系统级 API。
        /// </summary>
        private void ConfirmSelection(int index)
        {
            if (_currentActions.Count == 0) return;
            if (index < 0 || index >= _currentActions.Count) return;
            CampActionSO action = _currentActions[index];
            if (action == null) return;
            switch (action.ActionID)
            {
                case CampActionID.Crafting:
                    // 打开打造界面：由 CraftingUIController 自行控制 show/hide。
                    EventBus.Raise(new OpenCraftingUIEvent());
                    break;
                case CampActionID.Inventory:
                    // 与 ActionMenu 走相同事件通路，由 InventoryManager 统一处理打开逻辑。
                    EventBus.Raise(new OpenInventoryEvent());
                    break;
                case CampActionID.Memory:
                    EventBus.Raise(new OpenMemoryUIEvent());
                    break;
                case CampActionID.Equip:
                    // 与 Inventory/Memory 走相同事件通路，由 EquipmentUIController 自行控制 show/hide。
                    EventBus.Raise(new OpenEquipmentUIEvent());
                    break;
                case CampActionID.Training:
                    // TODO: 训练功能尚未实现，先用日志占位选中效果。
                    DebugTools.Log("<color=cyan>[露营菜单] 选中了【训练】（功能待实现）。</color>");
                    break;
                case CampActionID.Map:
                    // TODO: 地图功能尚未实现，先用日志占位选中效果。
                    DebugTools.Log("<color=cyan>[露营菜单] 选中了【地图】（功能待实现）。</color>");
                    break;
                case CampActionID.Sleep:
                    // 转发为业务事件：由 CampUIController 接管编排（黑屏 / 存档 / 返回棋盘）。
                    // View 不再直接编排跨系统流程，符合 MVB 模式职责分离。
                    DebugTools.Log("Log: 玩家请求 Sleep，事件已转发给 CampUIController。");
                    EventBus.Raise(new CampSleepRequestedEvent());
                    break;
            }
        }
    }
}
