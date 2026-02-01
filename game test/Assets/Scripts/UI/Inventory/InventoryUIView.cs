using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包 UI 视图：
    /// 负责显示/隐藏背包、刷新槽位列表、处理点击与关闭行为。
    /// </summary>
    public class InventoryUIView : MonoBehaviour
    {
        [Header("Binder")]
        // 绑定器：集中持有 UI 引用
        // Inspector 配置指南：
        // - 在 InventoryUIView 的 Binder 字段中拖拽 InventoryUIBinder
        // - 在 InventoryUIBinder 中设置 SlotPrefab（InventorySlotUI 预制体）
        // - ContentRoot 用于承载动态生成的槽位实例
        [SerializeField] private InventoryUIBinder binder;

        // 对外事件：关闭请求（可由上层监听）
        public event Action OnCloseRequested;
        // 对外事件：槽位点击（可由上层监听）
        public event Action<InventorySlot> OnSlotClicked;

        // --- 内部缓存 ---
        private readonly List<InventorySlotUI> _slots = new List<InventorySlotUI>();
        // CanvasGroup 控制（软隐藏）
        private CanvasGroup _canvasGroup;
        // 是否使用 CanvasGroup 模式
        private bool _useCanvasGroup = false;

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[InventoryUIView] Missing binder reference.");
                return;
            }
            if (binder.CloseButton != null)
            {
                // 绑定关闭按钮点击事件
                binder.CloseButton.onClick.AddListener(HandleCloseClicked);
            }
            SetupVisibility();
            SetVisible(false);
        }

        private void Start()
        {
            if (binder != null)
            {
                Transform root = binder.RootPanel != null ? binder.RootPanel.transform : transform;
                Canvas canvas = root.GetComponentInParent<Canvas>();
            }
        }

        private void OnEnable()
        {
            // 订阅背包更新/打开/关闭事件
            InventoryManager.OnInventoryUpdated += HandleRefresh;
            InventoryManager.OnInventoryOpened += HandleOpen;
            InventoryManager.OnInventoryClosed += HandleClose;
        }

        private void OnDisable()
        {
            // 退订事件，避免内存泄漏
            InventoryManager.OnInventoryUpdated -= HandleRefresh;
            InventoryManager.OnInventoryOpened -= HandleOpen;
            InventoryManager.OnInventoryClosed -= HandleClose;
        }

        /// <summary>
        /// 显示背包并刷新槽位内容。
        /// </summary>
        public void Show(List<InventorySlot> slots)
        {
            SetVisible(true);
            Rebuild(slots);
        }

        /// <summary>
        /// 隐藏背包。
        /// </summary>
        public void Hide()
        {
            SetVisible(false);
        }

        private void HandleCloseClicked()
        {
            // 通知外部关闭请求
            OnCloseRequested?.Invoke();
            // 通知背包管理器关闭
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null) inv.CloseInventory();
        }

        /// <summary>
        /// 根据物品列表重建槽位。
        /// </summary>
        private void Rebuild(List<InventorySlot> slots)
        {
            ClearSlots();
            if (slots == null || binder.SlotPrefab == null || binder.ContentRoot == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotUI slot = Instantiate(binder.SlotPrefab, binder.ContentRoot);
                slot.Setup(slots[i], HandleSlotClicked);
                _slots.Add(slot);
            }
        }

        private void HandleSlotClicked(InventorySlot slot)
        {
            // 对外广播点击事件
            OnSlotClicked?.Invoke(slot);
            // 直接调用背包管理器使用道具
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null && slot != null) inv.UseItem(slot.Item);
        }

        /// <summary>
        /// 背包内容更新回调。
        /// </summary>
        private void HandleRefresh(List<InventorySlot> slots)
        {
            Rebuild(slots);
        }

        /// <summary>
        /// 背包打开回调。
        /// </summary>
        private void HandleOpen()
        {
            SetVisible(true);
        }

        /// <summary>
        /// 背包关闭回调。
        /// </summary>
        private void HandleClose()
        {
            SetVisible(false);
        }

        private void SetupVisibility()
        {
            if (binder.RootPanel == null) return;
            if (binder.RootPanel == gameObject)
            {
                // 若根节点就是自身，则使用 CanvasGroup 控制显示
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                _useCanvasGroup = true;
            }
        }

        private void SetVisible(bool visible)
        {
            if (binder.RootPanel == null) return;
            if (_useCanvasGroup && _canvasGroup != null)
            {
                // 软隐藏：保持对象存在但不可交互
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
                return;
            }
            // 硬隐藏：直接启用/禁用对象
            binder.RootPanel.SetActive(visible);
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null) Destroy(_slots[i].gameObject);
            }
            _slots.Clear();
        }
    }
}
