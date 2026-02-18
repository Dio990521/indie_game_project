using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.UI.TitleScreen;

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
        [SerializeField] private Button loadButton;      // 读取存档按钮
        [SerializeField] private Slider loadingSlider;   // 加载进度条
        [SerializeField] private SaveLoadMenuView saveLoadMenuView; // 读档菜单视图（可选，通常由 EventBus 打开）

        [Header("Addressable 资源引用")]
        [Tooltip("玩家预制体的 Addressable 引用，在进入世界场景前预先加载到内存中。")]
        [SerializeField] private AssetReferenceGameObject playerPrefabReference;

        private bool _isLoading; // 状态锁：防止加载过程中重复点击按钮
        // 是否处于“标题读档后应自动进游戏”的等待状态。
        // 只有在收到 TitleLoadGameRequestedEvent 后才会置为 true。
        private bool _pendingStartFromLoad;

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

            if (loadButton != null)
            {
                // 绑定读取按钮点击事件
                loadButton.onClick.AddListener(HandleLoadClicked);
            }

            RefreshLoadButtonInteractivity();
        }

        private void OnEnable()
        {
            // 监听标题读档流程事件：
            // 1) 用户确认读取（进入等待自动进游戏状态）；
            // 2) 读档成功（触发进入游戏）；
            // 3) 读档失败（取消等待状态）。
            EventBus.Subscribe<TitleLoadGameRequestedEvent>(HandleTitleLoadGameRequestedEvent);
            EventBus.Subscribe<LoadCompletedEvent>(HandleLoadCompletedEvent);
            EventBus.Subscribe<LoadFailedEvent>(HandleLoadFailedEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TitleLoadGameRequestedEvent>(HandleTitleLoadGameRequestedEvent);
            EventBus.Unsubscribe<LoadCompletedEvent>(HandleLoadCompletedEvent);
            EventBus.Unsubscribe<LoadFailedEvent>(HandleLoadFailedEvent);
        }

        private void OnDestroy()
        {
            // 良好的编程习惯：销毁时注销按钮监听
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveListener(HandleLoadClicked);
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
            // 用户主动点“开始新游戏”时，明确取消“读档后自动进游戏”的等待状态。
            _pendingStartFromLoad = false;

            // New Game 语义：清理历史“读档快照缓存”，避免旧档状态误注入到新开局。
            SaveManager saveManager = FindAnyObjectByType<SaveManager>();
            if (saveManager != null)
            {
                saveManager.ClearLoadedStateCache();
            }

            // 启动核心加载协程
            StartCoroutine(BeginGameRoutine());
        }

        /// <summary>
        /// 点击“Load Game”入口：
        /// 通过 EventBus 打开读档菜单（解耦 Title 与具体菜单实现）。
        /// </summary>
        private void HandleLoadClicked()
        {
            if (_isLoading) return;

            if (EventBus.HasSubscribers<OpenSaveLoadMenuEvent>())
            {
                EventBus.Raise(new OpenSaveLoadMenuEvent());
                return;
            }

            // 兜底：若未走事件订阅模式，则尝试直接调用引用。
            if (saveLoadMenuView != null)
            {
                saveLoadMenuView.ShowSaveList();
            }
        }

        /// <summary>
        /// 收到“标题已确认读取存档”事件：
        /// 仅标记状态，不立即开场景。
        /// 这样能保证“先完成 LoadAsync（状态恢复）再进入 World”。
        /// </summary>
        private void HandleTitleLoadGameRequestedEvent(TitleLoadGameRequestedEvent evt)
        {
            _pendingStartFromLoad = true;
        }

        /// <summary>
        /// 标题读档成功后的自动进游戏入口：
        /// 只有在 _pendingStartFromLoad=true 时才触发，避免误响应其他 LoadCompletedEvent。
        /// </summary>
        private void HandleLoadCompletedEvent(LoadCompletedEvent evt)
        {
            if (!_pendingStartFromLoad) return;
            if (_isLoading)
            {
                // 若标题已在进行其他加载流程，则丢弃这次“读档自动开局”请求，避免状态残留。
                _pendingStartFromLoad = false;
                return;
            }

            _pendingStartFromLoad = false;
            StartCoroutine(BeginGameRoutine());
        }

        /// <summary>
        /// 标题读档失败处理：
        /// 取消“自动进游戏等待状态”，保留标题界面让玩家重新选择。
        /// </summary>
        private void HandleLoadFailedEvent(LoadFailedEvent evt)
        {
            if (!_pendingStartFromLoad) return;
            _pendingStartFromLoad = false;
            Debug.LogWarning($"[TitleScreenManager] Load failed for slot {evt.SlotIndex}: {evt.Error}");
        }

        /// <summary>
        /// 刷新 Load 按钮可交互状态：
        /// 若没有任何存档，则禁用按钮（可选需求）。
        /// </summary>
        private void RefreshLoadButtonInteractivity()
        {
            if (loadButton == null) return;
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                // SaveManager 缺失时保守禁用，避免点了无反应。
                loadButton.interactable = false;
                return;
            }

            bool hasAnySave = false;
            List<SaveMetaData> slots = saveManager.GetAllSaveSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                hasAnySave = true;
                break;
            }

            loadButton.interactable = hasAnySave;
        }

        /// <summary>
        /// 核心加载协程：按顺序执行资源预加载和场景加载，并实时更新进度条。
        /// </summary>
        private IEnumerator BeginGameRoutine()
        {
            _isLoading = true;

            // 1. UI 反馈：禁用按钮并显示进度条
            if (startButton != null) startButton.interactable = false;
            if (loadButton != null) loadButton.interactable = false;
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
                // 调用场景加载器的原始加载接口，获取 AsyncOperation 以驱动进度条
                AsyncOperation sceneOp = SceneLoader.Instance.LoadSceneAsyncRaw("World", null);

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
