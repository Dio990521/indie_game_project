using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Hud
{
    /// <summary>
    /// 玩家 HUD 视图层（View）：
    /// 只负责"如何显示"，不负责"何时显示/显示什么业务数据来源"。
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
        /// 控制 HUD 面板显示状态。
        /// 注意：RootNode 必须设为子节点（内层 Panel），不能是 Controller/View 所在的根对象，
        /// 否则 SetActive(false) 会同时禁用 Controller，导致后续事件收不到。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (binder == null || binder.RootNode == null)
            {
                Debug.LogWarning("[PlayerHudView] RootNode 未设置，SetVisible 被跳过。请在 Inspector 中将 RootNode 指向子 Panel。", this);
                return;
            }
            binder.RootNode.SetActive(visible);
        }

        /// <summary>
        /// 刷新头像显示。
        /// </summary>
        public void RefreshAvatar(Sprite avatar)
        {
            if (binder == null || binder.AvatarImage == null) return;
            binder.AvatarImage.sprite = avatar;
            // 无头像时隐藏图片组件，避免显示空白框
            binder.AvatarImage.enabled = avatar != null;
        }

        /// <summary>
        /// 刷新生命值显示：
        /// - 进度条显示 current/max；
        /// - 文本显示为 "current / max"。
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
        /// - 文本显示为 "current / required"。
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
        /// 刷新行动点显示：
        /// - 进度条显示 current/max；
        /// - 文本格式为 "current / max"，方便玩家直观看到剩余次数。
        /// </summary>
        public void RefreshActionPoints(int current, int max)
        {
            if (binder == null) return;

            float fill = CalculateFillAmount(current, max);
            if (binder.ApFillImage != null)
            {
                binder.ApFillImage.fillAmount = fill;
            }

            if (binder.ApValueText != null)
            {
                binder.ApValueText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(1, max)}";
            }
        }

        /// <summary>
        /// 刷新日期显示：
        /// 直接替换文本内容，格式由 DateSystem 传入（如 "第1年1月2日"）。
        /// </summary>
        public void RefreshDate(string formattedDate)
        {
            if (binder == null || binder.DateText == null) return;
            binder.DateText.text = formattedDate;
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
