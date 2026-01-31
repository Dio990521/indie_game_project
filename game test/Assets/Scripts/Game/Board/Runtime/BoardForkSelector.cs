using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Input;
using IndieGame.Gameplay.Board.View;
using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘分叉选择器：负责处理玩家在分叉路口时的交互逻辑。
    /// 包含输入监听、选项切换、UI 高亮更新以及最终路径的确认。
    /// </summary>
    public class BoardForkSelector : MonoBehaviour
    {
        [Header("外部依赖")]
        [Tooltip("负责读取玩家输入的组件（如键盘、手柄事件）")]
        public GameInputReader inputReader;

        [Tooltip("负责棋盘视觉表现的辅助类，用于显示和高亮路径光标")]
        public BoardViewHelper viewHelper;

        [Header("设置")]
        [Tooltip("输入延迟时间（秒）。用于防止玩家按住方向键时，选项切换速度过快。")]
        public float inputDelay = 0.2f;

        // 内部标记：记录玩家是否按下了“确认/交互”键
        private bool _interactTriggered = false;

        private void OnEnable()
        {
            // 订阅输入系统的交互事件：当按下确认键时，触发 OnInteractInput
            EventBus.Subscribe<InputInteractEvent>(OnInteractInput);
        }

        private void OnDisable()
        {
            // 注销订阅，避免内存泄漏或在对象禁用后执行逻辑
            EventBus.Unsubscribe<InputInteractEvent>(OnInteractInput);
        }

        /// <summary>
        /// 当输入系统检测到交互按键（如 Space, Enter）时调用。
        /// </summary>
        private void OnInteractInput(InputInteractEvent evt)
        {
            // 记录一次确认输入，由正在运行的选择协程在下一帧逻辑中“消费”掉
            _interactTriggered = true;
        }

        /// <summary>
        /// 外部调用：强行清理当前的选择状态和 UI 表现。
        /// </summary>
        public void ClearSelection()
        {
            _interactTriggered = false;
            // 通知视觉辅助类移除所有显示的路点光标
            if (viewHelper != null) viewHelper.ClearCursors();
        }

        /// <summary>
        /// 协程：开始进入分叉选择流程（重载：直接传入路点节点）。
        /// </summary>
        /// <param name="forkNode">发生分叉的节点</param>
        /// <param name="onSelected">选择完成后的回调动作</param>
        public IEnumerator SelectConnection(MapWaypoint forkNode, System.Action<WaypointConnection> onSelected)
        {
            // 获取该节点配置的所有连线作为选项
            List<WaypointConnection> options = forkNode != null ? forkNode.connections : null;
            return SelectConnection(forkNode, options, onSelected);
        }

        /// <summary>
        /// 协程核心逻辑：处理分叉路径的具体选择过程。
        /// </summary>
        /// <param name="forkNode">分叉起点节点</param>
        /// <param name="options">可供选择的连线列表</param>
        /// <param name="onSelected">选择完成后的回调，返回选中的连线</param>
        public IEnumerator SelectConnection(MapWaypoint forkNode, List<WaypointConnection> options, System.Action<WaypointConnection> onSelected)
        {
            // 1. 基础合法性检查
            if (forkNode == null || options == null || options.Count == 0)
            {
                onSelected?.Invoke(null);
                yield break;
            }

            // 2. 依赖检查
            if (inputReader == null || viewHelper == null)
            {
                Debug.LogWarning("[BoardForkSelector] 缺失 inputReader 或 viewHelper，无法进行分叉选择。");
                onSelected?.Invoke(null);
                yield break;
            }

            // --- 初始化选择状态 ---
            int currentIndex = 0; // 当前选中的选项索引
            bool selected = false; // 是否已点击确认

            _interactTriggered = false; // 重置交互标记，确保不继承之前的误触

            // 3. UI 初始化：在所有可能的路点上显示光标，并高亮默认的第一个选项
            viewHelper.ShowCursors(options, forkNode.transform.position);
            viewHelper.HighlightCursor(currentIndex);

            float nextInputTime = 0f; // 用于控制输入冷却时间

            // 等待一帧，防止开启协程的瞬间立刻捕获到上一状态的残留输入
            yield return null;

            // --- 循环监听输入 ---
            while (!selected)
            {
                // 读取当前的移动输入（通常是摇杆或 WASD）
                Vector2 moveInput = inputReader.CurrentMoveInput;

                // A. 处理选项切换逻辑（左右方向键）
                // 检查冷却时间，并判断水平输入是否超过阈值（0.5f 代表推杆过半）
                if (Time.time > nextInputTime && Mathf.Abs(moveInput.x) > 0.5f)
                {
                    // 根据输入方向增减索引
                    if (moveInput.x < 0) currentIndex--;
                    else currentIndex++;

                    // 循环索引：如果超出边界则跳到另一头
                    if (currentIndex < 0) currentIndex = options.Count - 1;
                    if (currentIndex >= options.Count) currentIndex = 0;

                    // 更新视觉反馈：切换高亮光标
                    viewHelper.HighlightCursor(currentIndex);

                    // 设置下一次可触发切换的时间点，防止选择“飞速跳过”
                    nextInputTime = Time.time + inputDelay;
                }

                // B. 处理确认逻辑
                // 如果 OnInteractInput 被触发，说明玩家按下了确认键
                if (_interactTriggered)
                {
                    selected = true;      // 跳出 while 循环
                    _interactTriggered = false; // 消费掉这次输入
                }

                // 每一帧挂起，等待下一帧继续轮询
                yield return null;
            }

            // --- 结束流程 ---
            // 4. 清理 UI：移除所有路径选择光标
            viewHelper.ClearCursors();

            // 5. 执行回调：将玩家最终选中的那条连线数据传回给调用者（通常是移动控制器）
            onSelected?.Invoke(options[currentIndex]);
        }
    }
}
