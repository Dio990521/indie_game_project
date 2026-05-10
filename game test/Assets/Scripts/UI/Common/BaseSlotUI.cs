using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IndieGame.UI.Common
{
    /// <summary>
    /// 可点击 Slot UI 的统一基类：
    /// - 实现 IPointerClickHandler，把所有 Slot 的“点击 → 业务”路径统一为 HandleClick；
    /// - 子类只需实现 HandleClick，不再各自重复 OnPointerClick 样板；
    /// - 提供静态 SetIcon / SetName 工具方法，避免 5 个 SlotUI 中重复的“null 检查 + 兜底文本”代码。
    ///
    /// 设计取舍：
    /// - 基类“不”声明 SerializeField 图标/名称字段。
    ///   原因是各 SlotUI 现存 prefab 把字段引用绑定到子类的具体字段名（nameText / nameLabel 不一致），
    ///   把字段提升到基类容易破坏现有 prefab 引用。
    /// - 因此基类只提供“行为”和“工具”，字段仍由子类自己声明并在 Inspector 绑定。
    /// </summary>
    public abstract class BaseSlotUI : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// 统一点击入口：转发到子类的 HandleClick。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData) => HandleClick(eventData);

        /// <summary>
        /// 子类实现具体的点击业务（一般是发布 EventBus 事件或调用回调）。
        /// </summary>
        protected abstract void HandleClick(PointerEventData eventData);

        /// <summary>
        /// 设置图标：null 时自动隐藏 Image 组件。
        /// </summary>
        protected static void SetIcon(Image image, Sprite icon)
        {
            if (image == null) return;
            image.sprite = icon;
            image.enabled = icon != null;
        }

        /// <summary>
        /// 设置 TMP 文本：空字符串时使用 fallback 兜底，避免显示空白。
        /// </summary>
        protected static void SetName(TMP_Text label, string displayName, string fallback = "Unknown")
        {
            if (label == null) return;
            label.text = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName;
        }
    }
}
