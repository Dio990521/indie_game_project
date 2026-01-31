using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using IndieGame.Core;

namespace IndieGame.UI
{
    /// <summary>
    /// 标题界面管理器：
    /// 负责处理游戏启动时的 UI 交互、资源预加载以及场景跳转。
    /// 通过协程实现了一个平滑的加载进度条，将资源加载与场景加载进度合并显示。
    /// </summary>
    public class TitleScreenManager : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Button startButton;     // 开始游戏按钮
        [SerializeField] private Slider loadingSlider;   // 加载进度条

        [Header("Addressable 资源引用")]
        [Tooltip("玩家预制体的 Addressable 引用，在进入世界场景前预先加载到内存中。")]
        [SerializeField] private AssetReferenceGameObject playerPrefabReference;

        private bool _isLoading; // 状态锁：防止加载过程中重复点击按钮

        // 存储 Addressable 加载操作的句柄，用于跟踪进度和后续的手动释放
        private AsyncOperationHandle<GameObject> _playerHandle;

        private void Awake()
        {
            // 初始化 UI 状态
            if (loadingSlider != null)
            {
                // 初始时隐藏进度条
                loadingSlider.gameObject.SetActive(false);
                loadingSlider.value = 0f;
            }

            if (startButton != null)
            {
                // 绑定点击事件
                startButton.onClick.AddListener(HandleStartClicked);
            }
        }

        private void OnDestroy()
        {
            // 良好的编程习惯：销毁时注销按钮监听
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
            }

            // [重要] 内存管理：如果 handle 是有效的，则释放 Addressable 资源引用
            // 防止标题界面销毁后产生内存泄漏
            if (_playerHandle.IsValid())
            {
                Addressables.Release(_playerHandle);
            }
        }

        /// <summary>
        /// 当用户点击“开始游戏”时触发。
        /// </summary>
        private void HandleStartClicked()
        {
            // 如果已经在加载中了，直接拦截
            if (_isLoading) return;

            // 启动核心加载协程
            StartCoroutine(BeginGameRoutine());
        }

        /// <summary>
        /// 核心加载协程：按顺序执行资源预加载和场景加载，并实时更新进度条。
        /// </summary>
        private IEnumerator BeginGameRoutine()
        {
            _isLoading = true;

            // 1. UI 反馈：禁用按钮并显示进度条
            if (startButton != null) startButton.interactable = false;
            if (loadingSlider != null)
            {
                loadingSlider.gameObject.SetActive(true);
                loadingSlider.value = 0f;
            }

            // --- 第一阶段：加载 Addressable 资源 (权重分配：占总进度的 20%) ---
            if (playerPrefabReference != null)
            {
                // 发起异步加载请求
                _playerHandle = playerPrefabReference.LoadAssetAsync<GameObject>();

                // 循环轮询直至资源加载完毕
                while (!_playerHandle.IsDone)
                {
                    if (loadingSlider != null)
                    {
                        // PercentComplete 返回 0 到 1 之间的值
                        // 映射公式：0.0 ~ 1.0 -> 0.0 ~ 0.2
                        loadingSlider.value = _playerHandle.PercentComplete * 0.2f;
                    }
                    // 等待下一帧继续检查
                    yield return null;
                }

                // 确保第一阶段结束时进度条至少达到 20%
                if (loadingSlider != null) loadingSlider.value = 0.2f;
            }

            // --- 第二阶段：加载场景 (权重分配：占总进度的 80%) ---
            if (SceneLoader.Instance != null)
            {
                // 调用封装好的场景加载器加载 "World" 场景
                // 这里期望 LoadScene 方法返回的是原生 Unity 的 AsyncOperation
                AsyncOperation sceneOp = SceneLoader.Instance.LoadScene("World", null);

                if (sceneOp != null)
                {
                    // [关键设置] 阻止场景加载完自动跳转
                    // 这样我们可以控制在进度条填满后再进行实际的场景激活
                    sceneOp.allowSceneActivation = false;

                    // Unity 的 sceneOp.progress 逻辑：
                    // 加载阶段进度为 0.0 到 0.9。当达到 0.9 时，表示场景已在后台加载完毕，等待激活。
                    while (sceneOp.progress < 0.9f)
                    {
                        if (loadingSlider != null)
                        {
                            // 将 0~0.9 的原生进度归一化为 0~1.0 的百分比
                            float sceneProgress = sceneOp.progress / 0.9f;

                            // 映射公式：将 0.0 ~ 1.0 映射到进度条的 0.2 ~ 1.0 区间
                            loadingSlider.value = 0.2f + (sceneProgress * 0.8f);
                        }
                        yield return null;
                    }

                    // 加载完毕后的视觉处理：强制将进度条拉满
                    if (loadingSlider != null) loadingSlider.value = 1f;

                    // 稍微停顿半秒，让玩家看清进度条已完成，增强交互的“确认感”
                    yield return new WaitForSeconds(0.5f);

                    // [最后一步] 允许激活场景，触发真正的画面跳转
                    sceneOp.allowSceneActivation = true;
                }
            }
        }
    }
}