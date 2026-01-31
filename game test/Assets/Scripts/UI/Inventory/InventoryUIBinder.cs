using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Inventory
{
    /// <summary>
    /// 背包 UI 绑定器：
    /// 统一收集背包界面所需的 UI 引用，避免在 View 中频繁查找。
    /// </summary>
    public class InventoryUIBinder : MonoBehaviour
    {
        // 背包根面板（用于显示/隐藏）
        [SerializeField] private GameObject rootPanel;
        // 物品槽容器（用于挂载槽位实例）
        [SerializeField] private Transform contentRoot;
        // 物品槽预制体（用于动态创建槽位）
        [SerializeField] private InventorySlotUI slotPrefab;
        // 关闭按钮
        [SerializeField] private Button closeButton;

        /// <summary> 根面板 </summary>
        public GameObject RootPanel => rootPanel;
        /// <summary> 内容容器 </summary>
        public Transform ContentRoot => contentRoot;
        /// <summary> 槽位预制体 </summary>
        public InventorySlotUI SlotPrefab => slotPrefab;
        /// <summary> 关闭按钮 </summary>
        public Button CloseButton => closeButton;
    }
}
