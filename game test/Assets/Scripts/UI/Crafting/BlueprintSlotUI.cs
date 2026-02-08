using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using IndieGame.Core;

namespace IndieGame.UI.Crafting
{
    /// <summary>
    /// 左侧图纸列表项 UI：
    /// 只负责“把数据渲染到控件上”，不包含选择逻辑与业务逻辑。
    ///
    /// 关键展示规范：
    /// - 左侧列表显示“图纸固定名称”（来自 BlueprintSO.DefaultName）
    /// - 图标通常显示成品图标（由 BlueprintSO 提供）
    /// </summary>
    public class BlueprintSlotUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;

        // 当前槽位绑定的“列表条目唯一键”（不是蓝图 ID）
        // 设计原因：
        // - 原型 Tab 一般是 1 蓝图 = 1 条目
        // - 复现 Tab 可能出现同一蓝图的多条历史记录（名称不同）
        // 因此点击事件不能只传蓝图 ID，必须传条目键来精确定位。
        private string _entryKey;
        // 当前条目对应的蓝图 ID（用于点击事件携带）
        private string _blueprintId;

        /// <summary>
        /// 设置列表项显示内容：
        /// - entryKey：UI 列表中的唯一条目标识
        /// - blueprintId：对应配方 ID
        /// - icon：显示图标（一般使用成品图标）
        /// - displayName：显示名称（原型 Tab 用原始名，复现 Tab 用自定义名）
        /// </summary>
        public void Setup(string entryKey, string blueprintId, Sprite icon, string displayName)
        {
            _entryKey = string.IsNullOrWhiteSpace(entryKey) ? string.Empty : entryKey;
            _blueprintId = string.IsNullOrWhiteSpace(blueprintId) ? string.Empty : blueprintId;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Unnamed Blueprint" : displayName;
            }
        }

        /// <summary>
        /// 实现“点击整个 Slot 即选中”：
        /// 不再依赖 Action 回调，改为纯 EventBus 广播。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_entryKey)) return;
            EventBus.Raise(new CraftBlueprintSlotClickedEvent
            {
                EntryKey = _entryKey,
                BlueprintID = _blueprintId
            });
        }
    }
}
