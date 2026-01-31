using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.UI;
using IndieGame.Core;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 背包管理器：作为单例存在，管理玩家的所有物品。
    /// 负责物品的存储、背包界面的开启与关闭，以及物品的使用逻辑分发。
    /// 继承自 MonoSingleton 以确保全局唯一访问。
    /// </summary>
    public class InventoryManager : MonoSingleton<InventoryManager>
    {
        // 覆盖单例属性：在加载新场景时销毁。
        // 这通常意味着背包数据由存档系统另行管理，或者背包仅存在于特定模式。
        protected override bool DestroyOnLoad => true;

        [Header("数据")]
        [Tooltip("当前背包中的物品列表。ItemSO 是基于 ScriptableObject 的物品配置文件。")]
        public List<ItemSO> items = new List<ItemSO>();

        // --- 事件回调 ---

        /// <summary> 当背包内容发生变化（或初次打开同步数据）时触发，传递最新的物品列表 </summary>
        public static event Action<List<ItemSO>> OnInventoryUpdated;

        /// <summary> 当背包界面被请求打开时触发 </summary>
        public static event Action OnInventoryOpened;

        /// <summary> 当背包界面被请求关闭时触发 </summary>
        public static event Action OnInventoryClosed;

        // 初始化标记
        private bool _isInitialized;

        private void OnEnable()
        {
            // 通过事件总线订阅“打开背包”事件。
            // 这种解耦方式允许任何地方（如棋盘菜单或快捷键）发出 OpenInventoryEvent 即可打开背包。
            EventBus.Subscribe<OpenInventoryEvent>(HandleOpenInventory);
        }

        private void OnDisable()
        {
            // 禁用时务必取消订阅，防止内存泄漏或无效引用
            EventBus.Unsubscribe<OpenInventoryEvent>(HandleOpenInventory);
        }

        /// <summary>
        /// 响应事件总线的打开背包请求。
        /// </summary>
        private void HandleOpenInventory(OpenInventoryEvent evt)
        {
            OpenInventory();
        }

        /// <summary>
        /// 执行初始化逻辑。
        /// </summary>
        public void Init()
        {
            if (_isInitialized) return;
            _isInitialized = true;
        }

        /// <summary>
        /// 打开背包：通知 UI 层级同步数据并显示界面。
        /// </summary>
        public void OpenInventory()
        {
            // 1. 发送物品数据同步事件，UI 将根据 items 列表渲染图标
            OnInventoryUpdated?.Invoke(items);
            // 2. 发送打开指令，触发 UI 动画或显示 Canvas
            OnInventoryOpened?.Invoke();
        }

        /// <summary>
        /// 关闭背包：通知 UI 层级隐藏界面。
        /// </summary>
        public void CloseInventory()
        {
            OnInventoryClosed?.Invoke();
        }

        /// <summary>
        /// 使用指定的物品。
        /// </summary>
        /// <param name="item">要使用的物品配置文件</param>
        public void UseItem(ItemSO item)
        {
            if (item == null) return;

            // 调用 ItemSO 中定义的具体使用逻辑。
            // 例如：加血、增加棋盘移动步数、或者触发特定的 BoardEvent。
            item.Use();

            // 注意：此处代码目前没有包含“消耗”逻辑。
            // 如果物品是消耗品，通常需在此处执行 items.Remove(item) 并再次调用 OnInventoryUpdated。
        }
    }
}