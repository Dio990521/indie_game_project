using UnityEngine;
using IndieGame.Core;
using IndieGame.UI.Confirmation;
using IndieGame.Gameplay.Board.Runtime;
using UnityEngine.Localization;
using UnityEngine.Serialization;

namespace IndieGame.Gameplay.Board.Data
{
    /// <summary>
    /// 区域地块类：当玩家经过或停留在此类地块时，会弹出确认窗口并跳转到特定的探索场景。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Tiles/Zone Tile")]
    public class ZoneTile : TileBase
    {
        [Header("场景跳转配置")]
        [Tooltip("目标场景的名称（需在 Build Settings 中注册）")]
        public string TargetSceneName;

        [Tooltip("目标位置 ID，用于定位玩家加载场景后的起始坐标")]
        public LocationID TargetLocationId;

        [Header("本地化内容")]
        [FormerlySerializedAs("ZoneName")]
        [Tooltip("区域的显示名称（例如：'迷失森林'）")]
        public LocalizedString ZoneName;

        [TextArea]
        [FormerlySerializedAs("Description")]
        [Tooltip("区域的详细描述信息")]
        public LocalizedString Description;

        [FormerlySerializedAs("EnterPrompt")]
        [Tooltip("进入询问的模板文案。建议配置为: '确定要进入 {0} 吗？'")]
        public LocalizedString EnterPrompt;

        /// <summary>
        /// 设置为 true 表示玩家即使只是“路过”此格（未耗尽步数），也会触发 OnEnter 逻辑。
        /// </summary>
        public override bool TriggerOnPass => true;

        /// <summary>
        /// 当玩家步数刚好在此地块耗尽并停下时调用。
        /// </summary>
        /// <param name="player">当前玩家的 GameObject 引用</param>
        public override void OnPlayerStop(GameObject player)
        {
            // 为了保持逻辑一致性，重定向到 OnEnter 处理。
            // 无论玩家是路过还是停下，触发的行为是相同的。
            OnEnter(player);
        }

        /// <summary>
        /// 核心逻辑：处理玩家进入/经过地块时的交互请求。
        /// </summary>
        /// <param name="player">当前玩家的 GameObject 引用</param>
        public override void OnEnter(GameObject player)
        {
            // 1. 获取区域名称的本地化字符串。
            // 如果 ZoneName 为空，则回退使用默认字符串 "this zone"。
            string zoneLabel = ZoneName != null ? ZoneName.GetLocalizedString() : "this zone";

            // 2. 初始化默认的消息内容（作为兜底）。
            string message = $"Enter {zoneLabel}?";

            // 3. 处理带参数的本地化请求。
            if (EnterPrompt != null)
            {
                // 将区域名称作为参数 [0] 注入到本地化模板（如 "Enter {0}?"）中。
                EnterPrompt.Arguments = new object[] { zoneLabel };
                // 动态生成最终符合当前语言语法的字符串。
                message = EnterPrompt.GetLocalizedString();
            }

            // 4. 发起 UI 确认请求。
            // 这通常会触发一个模态对话框，询问玩家是否确认跳转。
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = message,       // 确认框显示的文本
                OnConfirm = () =>        // 玩家点击“确认”后的操作
                {
                    // 弹窗为非阻塞：此处强制停止棋盘移动，避免协程继续消耗步数
                    var board = Gameplay.Board.Runtime.BoardGameManager.Instance;
                    if (board != null && board.movementController != null)
                    {
                        board.movementController.StopMoveImmediate();
                    }

                    // 获取场景加载单例
                    SceneLoader loader = SceneLoader.Instance;
                    if (loader == null) return;

                    // 执行场景跳转，并传递目标位置信息
                    loader.LoadScene(TargetSceneName, TargetLocationId);
                },
                OnCancel = null          // 玩家点击“取消”或关闭窗口的操作（此处不执行额外逻辑）
            });
        }
    }
}
