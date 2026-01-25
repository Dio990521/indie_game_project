using IndieGame.Core;
using IndieGame.Core.CameraSystem;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    public class InitState : BoardState
    {
        public override void OnEnter(BoardGameManager context)
        {
            SceneLoader loader = SceneLoader.Instance;
            if (loader != null && loader.HasPayload && loader.IsReturnToBoard && context.movementController != null)
            {
                context.movementController.SetCurrentNodeById(loader.TargetWaypointIndex);
                if (CameraManager.Instance != null && GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
                {
                    CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
                    CameraManager.Instance.WarpCameraToTarget();
                }
                loader.ClearPayload();
            }
            else
            {
                context.ResetToStart();
            }
        }

        public override void OnUpdate(BoardGameManager context)
        {
            // 在第一帧更新时切换状态，此时状态机的 _isTransitioning 已重置为 false
            context.ChangeState(new PlayerTurnState());
        }
    }
}
