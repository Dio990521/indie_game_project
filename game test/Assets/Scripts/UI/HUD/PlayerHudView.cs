using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Hud
{
    /// <summary>
    /// 玩家 HUD 视图层（View）：
    /// 只负责“如何显示”，不负责“何时显示/显示什么业务数据来源”。
    ///
    /// 设计边界：
    /// - 显示逻辑：进度条填充、文本更新、显隐控制；
    /// - 不订阅 EventBus；
    /// - 不做场景规则判断；
    /// - 不做玩家对象筛选。
    /// </summary>
    public class PlayerHudView : MonoBehaviour
    {
        [Header("Binder")]
        [SerializeField] private PlayerHudBinder binder;

        /// <summary>
        /// 控制 HUD 根节点显示状态。
        /// </summary>
        public void SetVisible(bool visible)
        {
            GameObject root = binder != null && binder.RootNode != null ? binder.RootNode : gameObject;
            root.SetActive(visible);
        }

        /// <summary>
        /// 刷新生命值显示：
        /// - 进度条显示 current/max；
        /// - 文本显示为 “current / max”。
        /// </summary>
        public void RefreshHealth(int current, int max)
        {
            if (binder == null) return;

            float fill = CalculateFillAmount(current, max);
            if (binder.HpFillImage != null)
            {
                binder.HpFillImage.fillAmount = fill;
            }

            if (binder.HpValueText != null)
            {
                binder.HpValueText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(1, max)}";
            }
        }

        /// <summary>
        /// 刷新经验值显示：
        /// - 进度条显示 current/required；
        /// - 文本显示为 “current / required”。
        /// </summary>
        public void RefreshExp(int current, int required)
        {
            if (binder == null) return;

            float fill = CalculateFillAmount(current, required);
            if (binder.ExpFillImage != null)
            {
                binder.ExpFillImage.fillAmount = fill;
            }

            if (binder.ExpValueText != null)
            {
                binder.ExpValueText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(1, required)}";
            }
        }

        /// <summary>
        /// 计算进度条比例并做安全约束。
        /// </summary>
        private static float CalculateFillAmount(int current, int max)
        {
            if (max <= 0) return 0f;
            return Mathf.Clamp01((float)Mathf.Max(0, current) / max);
        }
    }
}
