using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using IndieGame.Core;
using IndieGame.Gameplay.Crafting;

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

        // 当前槽位绑定的图纸 ID（点击时通过 EventBus 广播）
        private string _blueprintId;

        /// <summary>
        /// 设置列表项显示内容：
        /// - 图标来自 BlueprintSO（通常是成品图标）
        /// - 名称固定来自 BlueprintSO.DefaultName（图纸不支持自定义命名）
        /// </summary>
        public void Setup(BlueprintRecord record, BlueprintSO data)
        {
            _blueprintId = record != null && !string.IsNullOrWhiteSpace(record.ID)
                ? record.ID
                : (data != null ? data.ID : string.Empty);

            if (iconImage != null)
            {
                iconImage.sprite = data != null ? data.GetDisplayIcon() : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = data != null ? data.DefaultName : "Unnamed Blueprint";
            }
        }

        /// <summary>
        /// 实现“点击整个 Slot 即选中”：
        /// 不再依赖 Action 回调，改为纯 EventBus 广播。
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_blueprintId)) return;
            EventBus.Raise(new CraftBlueprintSlotClickedEvent
            {
                BlueprintID = _blueprintId
            });
        }
    }
}
