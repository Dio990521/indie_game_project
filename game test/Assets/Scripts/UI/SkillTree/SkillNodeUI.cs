using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Gameplay.SkillTree;

namespace IndieGame.UI.SkillTree
{
    /// <summary>
    /// 单个技能节点 UI 组件：
    /// 负责显示技能图标、名称、SP 花费，以及根据学习状态切换视觉样式。
    ///
    /// 约束：
    /// - 不订阅 EventBus；
    /// - 不持有 SkillDataSO 引用（由 View 传入并在 Setup 中读取后丢弃）；
    /// - 通过 OnNodeClicked 回调向外暴露点击事件。
    /// </summary>
    public class SkillNodeUI : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text spCostText;

        [Header("State Objects")]
        [Tooltip("已学习状态的视觉遮罩/高亮（绿色边框等）")]
        [SerializeField] private GameObject learnedOverlay;
        [Tooltip("可学习状态的视觉提示（亮色边框）")]
        [SerializeField] private GameObject availableOverlay;
        [Tooltip("锁定状态的灰色遮罩")]
        [SerializeField] private GameObject lockedOverlay;

        [Header("Interaction")]
        [SerializeField] private Button clickButton;

        // 当前绑定的技能 ID（供点击回调使用）
        private string _currentSkillId;

        // 点击后通知外部的回调，由 View 注入
        public Action<string> OnNodeClicked;

        private void Awake()
        {
            if (clickButton != null)
                clickButton.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (clickButton != null)
                clickButton.onClick.RemoveListener(HandleClicked);
        }

        /// <summary>
        /// 绑定技能数据并刷新显示与状态。
        /// 由 SkillTreeView 在重建网格时调用。
        /// </summary>
        public void Setup(SkillDataSO data, SkillLearnState state)
        {
            if (data == null) return;

            _currentSkillId = data.SkillId;

            if (iconImage != null)
            {
                iconImage.sprite  = data.Icon;
                iconImage.enabled = data.Icon != null;
            }

            if (nameText != null)
                nameText.text = data.SkillName;

            RefreshCostText(state, data.SpCost);
            ApplyStateVisual(state);

            if (clickButton != null)
                clickButton.interactable = state != SkillLearnState.Locked;
        }

        /// <summary>
        /// 仅刷新节点的状态视觉（SP 数据未变时使用，避免重建整个节点）。
        /// </summary>
        public void RefreshState(SkillLearnState state)
        {
            ApplyStateVisual(state);
            if (clickButton != null)
                clickButton.interactable = state != SkillLearnState.Locked;
            if (state == SkillLearnState.Learned && spCostText != null)
                spCostText.text = "已学";
        }

        private void RefreshCostText(SkillLearnState state, int spCost)
        {
            if (spCostText == null) return;
            spCostText.text = state == SkillLearnState.Learned ? "已学" : $"{spCost} SP";
        }

        private void ApplyStateVisual(SkillLearnState state)
        {
            if (learnedOverlay != null)
                learnedOverlay.SetActive(state == SkillLearnState.Learned);
            if (availableOverlay != null)
                availableOverlay.SetActive(state == SkillLearnState.Available);
            if (lockedOverlay != null)
                lockedOverlay.SetActive(state == SkillLearnState.Locked);
        }

        private void HandleClicked()
        {
            if (!string.IsNullOrWhiteSpace(_currentSkillId))
                OnNodeClicked?.Invoke(_currentSkillId);
        }
    }
}
