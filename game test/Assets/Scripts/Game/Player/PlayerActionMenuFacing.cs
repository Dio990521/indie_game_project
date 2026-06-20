using System.Collections.Generic;
using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Runtime;
using UnityEngine;

namespace IndieGame.Gameplay.Player
{
    /// <summary>
    /// 玩家操作菜单朝向控制：
    /// 操作菜单显示时，让角色平滑转身面向当前主相机镜头；
    /// 玩家投掷骰子后，立即转身面向即将前进的方向（与镜头拉远同时进行）；
    /// 若前方是岔路/死路（方向还不确定），则不强行转向，交由移动协程开始后
    /// 的 Slerp 逻辑接管朝向。
    /// </summary>
    [RequireComponent(typeof(BoardEntity))]
    public class PlayerActionMenuFacing : MonoBehaviour
    {
        [Header("Facing")]
        [Tooltip("转向相机/行进方向的平滑参数")]
        [SerializeField] private SmoothFacingTweenOptions facingOptions = SmoothFacingTweenOptions.Default;

        private Tween _facingTween;
        private BoardEntity _boardEntity;

        private void Awake()
        {
            _boardEntity = GetComponent<BoardEntity>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BoardActionMenuShownEvent>(HandleMenuShown);
            EventBus.Subscribe<BoardRollDiceRequestedEvent>(HandleRollDiceRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BoardActionMenuShownEvent>(HandleMenuShown);
            EventBus.Unsubscribe<BoardRollDiceRequestedEvent>(HandleRollDiceRequested);
            SmoothFacingTween.Kill(ref _facingTween);
        }

        /// <summary>
        /// 操作菜单显示：转身面向主相机镜头。
        /// </summary>
        private void HandleMenuShown(BoardActionMenuShownEvent evt)
        {
            if (evt.Target != transform) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            SmoothFacingTween.TryRotateToWorldPosition(
                transform,
                mainCam.transform.position,
                ref _facingTween,
                facingOptions);
        }

        /// <summary>
        /// 投掷骰子：停止朝向相机的 Tween，转而尝试立即转身面向即将前进的方向，
        /// 让转身动作和镜头拉远同时发生。
        /// </summary>
        private void HandleRollDiceRequested(BoardRollDiceRequestedEvent evt)
        {
            SmoothFacingTween.Kill(ref _facingTween);
            TryFaceUpcomingMovementDirection();
        }

        /// <summary>
        /// 查询当前节点的有效出口：若方向唯一（没有岔路/死路），立即转身面向那个方向；
        /// 否则方向还不确定，不强行转向，交由移动协程开始后的 Slerp 逻辑接管。
        /// </summary>
        private void TryFaceUpcomingMovementDirection()
        {
            if (_boardEntity == null || _boardEntity.CurrentNode == null) return;

            List<MapWaypoint> validNodes = _boardEntity.CurrentNode.GetValidNextNodes(_boardEntity.LastWaypoint);
            if (validNodes == null || validNodes.Count != 1) return;

            SmoothFacingTween.TryRotateToWorldPosition(
                transform,
                validNodes[0].transform.position,
                ref _facingTween,
                facingOptions);
        }
    }
}
