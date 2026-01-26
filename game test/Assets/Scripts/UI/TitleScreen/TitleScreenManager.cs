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
        
        // 在 TitleScreenManager.cs 中
        private IEnumerator BeginGameRoutine()
        {
            _isLoading = true;
            if (startButton != null) startButton.interactable = false;
            if (loadingSlider != null)
            {
                loadingSlider.gameObject.SetActive(true);
                loadingSlider.value = 0f;
            }

            // --- 第一阶段：加载 Addressable 资源 (假设占总进度的 20%) ---
            if (playerPrefabReference != null)
            {
                _playerHandle = playerPrefabReference.LoadAssetAsync<GameObject>();
                while (!_playerHandle.IsDone)
                {
                    if (loadingSlider != null)
                    {
                        // 将 0~1 的进度映射到 0~0.2
                        loadingSlider.value = _playerHandle.PercentComplete * 0.2f; 
                    }
                    yield return null;
                }
            }

            // --- 第二阶段：加载场景 (假设占总进度的 80%) ---
            if (SceneLoader.Instance != null)
            {
                // 获取刚才修改后返回的 AsyncOperation
                AsyncOperation sceneOp = SceneLoader.Instance.LoadScene("World", null);
                
                if (sceneOp != null)
                {
                    sceneOp.allowSceneActivation = false; // 阻止场景加载完自动跳转，防止进度条没跑完就切了

                    // 当 progress < 0.9 时，表示正在加载。 >= 0.9 表示加载完毕，准备切换。
                    while (sceneOp.progress < 0.9f)
                    {
                        if (loadingSlider != null)
                        {
                            // sceneOp.progress 最大只有 0.9
                            float sceneProgress = sceneOp.progress / 0.9f; 
                            // 将场景进度映射到 0.2 ~ 1.0 之间
                            loadingSlider.value = 0.2f + (sceneProgress * 0.8f);
                        }
                        yield return null;
                    }

                    // 稍微停顿一下让进度条看起来走满了(可选)
                    if (loadingSlider != null) loadingSlider.value = 1f;
                    yield return new WaitForSeconds(0.5f);

                    // 允许切换场景
                    sceneOp.allowSceneActivation = true;
                }
            }
        }
    }
}
