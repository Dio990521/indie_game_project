using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using IndieGame.Core;

namespace IndieGame.UI
{
    public class TitleScreenManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button startButton;
        [SerializeField] private Slider loadingSlider;

        [Header("Addressables")]
        [Tooltip("Addressable reference for Player Prefab.")]
        [SerializeField] private AssetReferenceGameObject playerPrefabReference;

        private bool _isLoading;
        private AsyncOperationHandle<GameObject> _playerHandle;

        private void Awake()
        {
            if (loadingSlider != null)
            {
                loadingSlider.gameObject.SetActive(false);
                loadingSlider.value = 0f;
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(HandleStartClicked);
            }
        }

        private void OnDestroy()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
            }

            if (_playerHandle.IsValid())
            {
                Addressables.Release(_playerHandle);
            }
        }

        private void HandleStartClicked()
        {
            if (_isLoading) return;
            StartCoroutine(BeginGameRoutine());
        }

        private IEnumerator BeginGameRoutine()
        {
            _isLoading = true;
            if (startButton != null) startButton.interactable = false;
            if (loadingSlider != null)
            {
                loadingSlider.gameObject.SetActive(true);
                loadingSlider.value = 0f;
            }

            if (playerPrefabReference != null)
            {
                _playerHandle = playerPrefabReference.LoadAssetAsync<GameObject>();
                while (!_playerHandle.IsDone)
                {
                    if (loadingSlider != null)
                    {
                        loadingSlider.value = _playerHandle.PercentComplete;
                    }
                    yield return null;
                }
            }

            if (loadingSlider != null) loadingSlider.value = 1f;

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene("World", null);
            }
        }
    }
}
