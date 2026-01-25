using UnityEngine;
using UnityEngine.SceneManagement;
using IndieGame.Core.Utilities;

namespace IndieGame.Core
{
    public class SceneLoader : MonoSingleton<SceneLoader>
    {
        [Header("Board Scene")]
        [SerializeField] private string boardSceneName = "World";

        private struct TransitionPayload
        {
            public string SceneName;
            public LocationID TargetLocation;
            public int WaypointIndex;
            public bool ReturnToBoard;
        }

        private bool _hasPayload;
        private TransitionPayload _payload;

        public bool HasPayload => _hasPayload;
        public bool IsReturnToBoard => _hasPayload && _payload.ReturnToBoard;
        public LocationID TargetLocationId => _payload.TargetLocation;
        public int TargetWaypointIndex => _payload.WaypointIndex;
        public string TargetSceneName => _payload.SceneName;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public void LoadScene(string sceneName, LocationID targetID)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            _payload = new TransitionPayload
            {
                SceneName = sceneName,
                TargetLocation = targetID,
                WaypointIndex = -1,
                ReturnToBoard = false
            };
            _hasPayload = true;

            SceneManager.LoadSceneAsync(sceneName);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.FreeRoam);
            }
        }

        public void ReturnToBoard(int waypointIndex)
        {
            _payload = new TransitionPayload
            {
                SceneName = boardSceneName,
                TargetLocation = null,
                WaypointIndex = waypointIndex,
                ReturnToBoard = true
            };
            _hasPayload = true;

            SceneManager.LoadSceneAsync(boardSceneName);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.BoardMode);
            }
        }

        public void ClearPayload()
        {
            _hasPayload = false;
            _payload = default;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_hasPayload) return;
            EventBus.Raise(new SceneTransitionEvent
            {
                SceneName = scene.name,
                TargetLocation = _payload.TargetLocation,
                WaypointIndex = _payload.WaypointIndex,
                ReturnToBoard = _payload.ReturnToBoard
            });
        }
    }
}
