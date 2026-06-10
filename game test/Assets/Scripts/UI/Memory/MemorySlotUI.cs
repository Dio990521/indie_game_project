using IndieGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using IndieGame.UI.Common;

namespace IndieGame.UI.Memory
{
    /// <summary>
    /// Memory 图鉴列表通用 Slot UI：
    /// 所有 Tab 复用同一个预制体，布局为 [图标] + 主名称 + 副标签（可选）。
    /// 点击后广播 MemorySlotClickedEvent，由 MemoryUIController 路由详情刷新。
    /// </summary>
    public class MemorySlotUI : BaseSlotUI
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        // 副标签：如"持有中/已消耗"、分类名、"语料"等；可为空
        [SerializeField] private TMP_Text subtitleText;

        private string _entryKey;

        /// <summary>
        /// 设置 Slot 显示内容。
        /// </summary>
        public void Setup(string entryKey, Sprite icon, string displayName, string subtitle = "")
        {
            _entryKey = entryKey ?? string.Empty;
            SetIcon(iconImage, icon);
            SetName(nameText, displayName, "Unknown");
            if (subtitleText != null)
                subtitleText.text = subtitle ?? string.Empty;
        }

        protected override void HandleClick(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_entryKey)) return;
            EventBus.Raise(new MemorySlotClickedEvent { EntryKey = _entryKey });
        }
    }
}
