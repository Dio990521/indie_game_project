using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Board.View;
using IndieGame.Gameplay.Treasure;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 飞翼宝具激活状态：
    /// 负责收集目标候选格、展示高亮、等待玩家选择落点、执行抛物线飞跃、触发落地格子效果。
    /// 由 PlayerTurnState 在收到 TreasureItemSelectedEvent 后切换到此状态。
    /// </summary>
    public class WingTreasureState : BoardState
    {
        private readonly WingTreasureSO _data;
        private Coroutine _routine;

        public WingTreasureState(WingTreasureSO data)
        {
            _data = data;
        }

        public override void OnEnter(BoardGameManager context)
        {
            // 行动力二次检查（防止极端情况下数值被其他系统消耗）
            if (_data == null ||
                ActionPointSystem.Instance == null ||
                !ActionPointSystem.Instance.CanConsume(_data.ActionPointCost))
            {
                DebugTools.Log("[WingTreasureState] 行动力不足或数据缺失，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            BoardEntity entity = context.movementController?.PlayerEntity;
            if (entity == null || entity.CurrentNode == null)
            {
                DebugTools.LogWarning("[WingTreasureState] 找不到玩家实体或当前节点，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _routine = context.StartCoroutine(WingRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }

            // 确保高亮被清除（OnExit 异常退出时的保险）
            BoardViewHelper viewHelper = GetViewHelper(context);
            if (viewHelper != null) viewHelper.ClearCursors();
        }

        // ── 核心协程 ──────────────────────────────────────────────────────

        private IEnumerator WingRoutine(BoardGameManager context)
        {
            BoardEntity entity = context.movementController.PlayerEntity;
            BoardViewHelper viewHelper = GetViewHelper(context);

            // 收集可选目标格（前方 + 后方各 1 格）
            List<MapWaypoint> candidates = CollectTargetNodes(entity);

            if (candidates.Count == 0)
            {
                DebugTools.Log("[WingTreasureState] 无可用落点，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 在候选格上显示光标
            if (viewHelper != null)
            {
                viewHelper.ShowCursorsAtNodes(candidates);
                viewHelper.HighlightCursor(0);
            }

            // 等待玩家选择目标格（左右方向键 + 确认/取消）
            MapWaypoint pickedNode = null;
            bool cancelled = false;

            yield return WaitForTargetSelection(
                context,
                candidates,
                viewHelper,
                result => pickedNode = result,
                () => cancelled = true
            );

            // 清理高亮
            if (viewHelper != null) viewHelper.ClearCursors();

            if (cancelled || pickedNode == null)
            {
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 消耗行动力
            if (!ActionPointSystem.Instance.TryConsumeActionPoints(_data.ActionPointCost, "WingTreasure"))
            {
                DebugTools.Log("[WingTreasureState] 行动力不足，取消飞翼。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 记录起飞节点：用于落地后恢复 LastWaypoint，固定行进方向（下次掷骰不出现岔路UI）
            MapWaypoint originNode = entity.CurrentNode;

            // 执行抛物线飞跃动画（复用大炮格 LaunchParabolic）
            entity.SetMovingState(true);
            entity.SetMoveAnimationSpeed(0f);

            // 传入 originNode 使落地后 LastWaypoint = originNode，保留方向信息
            yield return entity.LaunchParabolic(pickedNode, _data.ArcHeight, _data.LaunchSpeed, originNode);

            // HandleExternalArrival 订阅完整事件管线（传送、大炮、方向等），并在 FinishMove 内重置 IsMoving/AnimSpeed
            yield return context.movementController.HandleExternalArrival(pickedNode);

            // 返回玩家回合
            context.ChangeState(new PlayerTurnState());
        }

        // ── 工具方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 收集飞翼可选目标节点：前方所有有效出口 + 后方来路节点，去重排列。
        /// </summary>
        private List<MapWaypoint> CollectTargetNodes(BoardEntity entity)
        {
            var result = new HashSet<MapWaypoint>();

            MapWaypoint current = entity.CurrentNode;
            MapWaypoint last = entity.LastWaypoint;

            // 前方：当前节点的有效出口（已排除来路，取邻居格）
            List<MapWaypoint> forwardNodes = current.GetValidNextNodes(last);
            foreach (var node in forwardNodes)
                if (node != null) result.Add(node);

            // 后方：来路节点（若存在）
            if (last != null) result.Add(last);

            return new List<MapWaypoint>(result);
        }

        /// <summary>
        /// 协程：等待玩家用方向键切换高亮格，并按确认或取消做出选择。
        /// </summary>
        private IEnumerator WaitForTargetSelection(
            BoardGameManager context,
            List<MapWaypoint> candidates,
            BoardViewHelper viewHelper,
            System.Action<MapWaypoint> onPicked,
            System.Action onCancelled)
        {
            // 获取 InputReader（来自 ForkSelector，零额外配置）
            IndieGame.Core.Input.GameInputReader inputReader =
                context.movementController.forkSelector?.inputReader;

            if (inputReader == null)
            {
                DebugTools.LogWarning("[WingTreasureState] 找不到 GameInputReader，无法进行目标选择。");
                onCancelled?.Invoke();
                yield break;
            }

            int currentIndex = 0;
            bool interactTriggered = false;
            bool cancelTriggered = false;

            System.Action<InputInteractEvent> onInteract = _ => interactTriggered = true;
            // 使用专用取消键（ESC / 手柄 B）而非 Interact 键的 release 事件。
            // InputInteractCanceledEvent 代表 Interact 键松开，玩家在 TreasureMenu 按下确认后
            // 松手就会触发，导致状态立即退出；UICancelEvent 是独立的取消绑定，不会误触。
            System.Action onCancel = () => cancelTriggered = true;

            // 等一帧，确保上一状态的确认按下事件不会被 onInteract 立即捕获
            yield return null;

            EventBus.Subscribe(onInteract);
            inputReader.UICancelEvent += onCancel;

            float nextInputTime = 0f;

            while (!interactTriggered && !cancelTriggered)
            {
                Vector2 moveInput = inputReader.CurrentMoveInput;

                if (Time.time > nextInputTime && Mathf.Abs(moveInput.x) > 0.5f)
                {
                    currentIndex += moveInput.x > 0 ? 1 : -1;
                    if (currentIndex < 0) currentIndex = candidates.Count - 1;
                    if (currentIndex >= candidates.Count) currentIndex = 0;

                    if (viewHelper != null) viewHelper.HighlightCursor(currentIndex);
                    nextInputTime = Time.time + context.movementController.forkSelector.inputDelay;
                }

                yield return null;
            }

            EventBus.Unsubscribe(onInteract);
            inputReader.UICancelEvent -= onCancel;

            if (cancelTriggered)
                onCancelled?.Invoke();
            else
                onPicked?.Invoke(candidates[currentIndex]);
        }

        /// <summary>通过 movementController.forkSelector.viewHelper 获取视觉辅助类。</summary>
        private static BoardViewHelper GetViewHelper(BoardGameManager context)
        {
            return context.movementController?.forkSelector?.viewHelper;
        }
    }
}
