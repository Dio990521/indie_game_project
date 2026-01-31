using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace IndieGame.Gameplay.Exploration
{
    /// <summary>
    /// 场景出口触发器：
    /// 当玩家进入挂载了该脚本的触发区域（Trigger）时，会弹出一个确认框，
    /// 询问玩家是否离开当前探索区域并返回棋盘模式。
    /// </summary>
    public class ExitZoneTrigger : MonoBehaviour
    {
        [Header("本地化设置")]
        [FormerlySerializedAs("ZoneName")]
        [Tooltip("当前区域的名称（用于在弹窗中显示，支持多语言）")]
        public LocalizedString ZoneName;

        [FormerlySerializedAs("LeavePrompt")]
        [Tooltip("离开时的确认语模板（例如：\"确认离开 {0} 吗？\"）")]
        public LocalizedString LeavePrompt;

        /// <summary>
        /// Unity 物理触发回调：当有物体进入触发器时执行。
        /// </summary>
        /// <param name="other">进入区域的碰撞体</param>
        private void OnTriggerEnter(Collider other)
        {
            // 1. 标签检查：确保触发者是玩家本人，忽略 NPC 或其他物理对象
            if (!other.CompareTag("Player")) return;

            // 2. 准备弹窗文本：
            // 尝试获取本地化的区域名称，如果没有配置则默认为 "Board"
            string zoneLabel = ZoneName != null ? ZoneName.GetLocalizedString() : "Board";
            string message = $"Leave {zoneLabel}?"; // 默认英文兜底信息

            // 如果配置了本地化的提问模板
            if (LeavePrompt != null)
            {
                // 将区域名称作为参数注入到本地化字符串中（替换模板中的 {0}）
                LeavePrompt.Arguments = new object[] { zoneLabel };
                message = LeavePrompt.GetLocalizedString();
            }

            // 3. 发起确认请求：
            // 调用 UI 系统的确认弹窗模块，传入显示信息和回调逻辑
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = message, // 弹窗显示的文本内容内容

                // 当玩家点击“确定”按钮时执行的回调
                OnConfirm = () =>
                {
                    // 获取全局场景加载器
                    SceneLoader loader = SceneLoader.Instance;
                    if (loader == null) return;

                    // 执行返回棋盘的方法：
                    // 该方法内部通常会处理场景跳转，并根据存档或内存标记恢复玩家在棋盘上的地块位置
                    loader.ReturnToBoard();
                },

                // 当玩家点击“取消”按钮时执行的回调（此处设为 null 表示仅关闭弹窗，不执行额外逻辑）
                OnCancel = null
            });
        }
    }
}