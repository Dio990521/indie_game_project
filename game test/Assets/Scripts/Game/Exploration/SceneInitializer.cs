using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;

namespace IndieGame.Gameplay.Exploration
{
    public class SceneInitializer : MonoBehaviour
    {
        private void Start()
        {
            SceneLoader loader = SceneLoader.Instance;
            if (loader == null || !loader.HasPayload || loader.IsReturnToBoard) return;

            LocationID target = loader.TargetLocationId;
            if (target == null)
            {
                loader.ClearPayload();
                return;
            }

            if (!SpawnPoint.TryGet(target, out SpawnPoint spawn))
            {
                Debug.LogWarning("[SceneInitializer] SpawnPoint not found for LocationID.");
                loader.ClearPayload();
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentPlayer == null)
            {
                Debug.LogWarning("[SceneInitializer] Player not ready.");
                loader.ClearPayload();
                return;
            }

            GameObject player = GameManager.Instance.CurrentPlayer;
            player.transform.position = spawn.transform.position;
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(player.transform);
                CameraManager.Instance.WarpCameraToTarget();
            }

            loader.ClearPayload();
        }
    }
}
