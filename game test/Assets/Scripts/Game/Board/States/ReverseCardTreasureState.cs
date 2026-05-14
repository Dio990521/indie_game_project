using System.Collections;
using DG.Tweening;
using UnityEngine;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Treasure;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 反转牌宝具激活状态：
    /// 消耗行动点 → 反转行进方向 → 播放原地跳跃 + Y轴旋转180°动画 → 返回 PlayerTurnState。
    /// ReversePlayerDirection() 已处理死胡同边界情况（设置 _allowFirstStepUTurn = true）。
    /// </summary>
    public class ReverseCardTreasureState : BoardState
    {
        private readonly ReverseCardTreasureSO _data;

        // 协程引用：OnExit 时若状态被意外中断可安全停止
        private Coroutine _routine;

        // DOTween Sequence 引用：OnExit 时 Kill，防止跨状态的 Tween 残留
        private Sequence _sequence;

        public ReverseCardTreasureState(ReverseCardTreasureSO data)
        {
            _data = data;
        }

        public override void OnEnter(BoardGameManager context)
        {
            // 行动力二次检查（防止极端情况下数值被其他系统提前消耗）
            if (_data == null ||
                ActionPointSystem.Instance == null ||
                !ActionPointSystem.Instance.CanConsume(_data.ActionPointCost))
            {
                DebugTools.Log("[ReverseCardTreasureState] 行动力不足或数据缺失，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            // 实体有效性检查
            BoardEntity entity = context.movementController?.PlayerEntity;
            if (entity == null || entity.CurrentNode == null)
            {
                DebugTools.LogWarning("[ReverseCardTreasureState] 找不到玩家实体或当前节点，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _routine = context.StartCoroutine(ReverseCardRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }

            // 终止 DOTween，防止对象销毁后 Tween 继续驱动 transform
            _sequence?.Kill();
            _sequence = null;
        }

        private IEnumerator ReverseCardRoutine(BoardGameManager context)
        {
            // 1. 消耗行动力
            if (!ActionPointSystem.Instance.TryConsumeActionPoints(_data.ActionPointCost, "ReverseCard"))
            {
                DebugTools.Log("[ReverseCardTreasureState] 行动力消耗失败，取消反转。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 2. 立即反转方向：修改 LastWaypoint，使下次 GetValidNextNodes 返回反向节点
            //    使用控制器层方法而非直接调用 entity.ReverseDirection()，
            //    因为 ReversePlayerDirection() 额外处理了死胡同情况（_allowFirstStepUTurn = true）
            context.movementController.ReversePlayerDirection();
            DebugTools.Log("[ReverseCardTreasureState] 行进方向已反转。");

            // 3. 播放原地动画：跳跃 + Y轴累加旋转180°，通过 Sequence.Join 同步执行
            Transform t = context.movementController.PlayerEntity.transform;

            _sequence = DOTween.Sequence();

            // 原地跳跃：目标落点 = 当前位置，实现弹起落回效果
            _sequence.Join(
                t.DOJump(t.position, _data.JumpHeight, 1, _data.AnimationDuration)
            );

            // Y轴累加旋转180°：RotateMode.LocalAxisAdd 确保相对当前朝向叠加，不受绝对角度影响
            _sequence.Join(
                t.DORotate(new Vector3(0f, 180f, 0f), _data.AnimationDuration, RotateMode.LocalAxisAdd)
            );

            yield return _sequence.WaitForCompletion();

            DebugTools.Log("[ReverseCardTreasureState] 反转动画完成，返回玩家回合。");

            // 4. 返回玩家回合，重新显示 action menu
            context.ChangeState(new PlayerTurnState());
        }
    }
}
