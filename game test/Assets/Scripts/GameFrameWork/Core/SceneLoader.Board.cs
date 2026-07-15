using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using IndieGame.Core.Utilities;
using IndieGame.UI;

namespace IndieGame.Core
{
    /// <summary>
    /// SceneLoader 的棋盘场景策略 partial：
    /// 包含"棋盘常驻"架构的全部实现——加载/恢复棋盘、根物体显隐、ActiveScene 切换、
    /// 棋盘场景名/引用解析、返回棋盘时的模式广播。
    /// 主流程入口与转场 Token 管理见 SceneLoader.cs，探索场景叠加策略见 SceneLoader.Exploration.cs。
    /// </summary>
    public partial class SceneLoader
    {
        private void CacheBoardNodeIndex()
        {
            if (sceneRegistry != null)
            {
                string currentScene = SceneManager.GetActiveScene().name;
                if (sceneRegistry.GetGameMode(currentScene) == GameMode.Board)
                {
                    _lastBoardSceneName = currentScene;
                }
            }
            // 从棋盘控制器读取当前节点，保存以便返回时复位
            var board = Gameplay.Board.Runtime.BoardGameManager.Instance;
            if (board == null || board.movementController == null) return;
            _lastBoardNodeIndex = board.movementController.CurrentNodeId;
        }

        /// <summary>
        /// 加载/恢复棋盘场景：
        /// - 若当前叠加了探索，则先卸载探索，再显示棋盘
        /// - 若已经在棋盘，则仅恢复根物体
        /// - 否则 Single 方式切换到棋盘
        /// </summary>
        private AsyncOperation LoadBoardScene(string sceneName, int transitionToken)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(_currentExplorationScene))
            {
                Scene exploration = SceneManager.GetSceneByName(_currentExplorationScene);
                if (exploration.IsValid() && exploration.isLoaded)
                {
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(exploration);
                    if (unloadOp != null)
                    {
                        unloadOp.completed += _ =>
                        {
                            // M1 修复：本回调可能在转场被打断、新转场已启动后才到达，
                            // 过期时放弃全部副作用，避免踩踏新转场的状态。
                            if (IsStaleTransition(transitionToken)) return;

                            // 卸载完成后恢复棋盘显示并激活
                            _currentExplorationScene = null;
                            SetBoardSceneRootsActive(true);
                            // 返回棋盘时重新显示玩家
                            if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
                            {
                                GameManager.Instance.CurrentPlayer.SetActive(true);
                            }
                            ActivateBoardScene();
                            RaiseBoardModeChanged();
                            CompleteTransition(transitionToken);
                        };
                    }
                    else
                    {
                        CompleteTransition(transitionToken);
                    }
                    return unloadOp;
                }
                _currentExplorationScene = null;
            }

            if (IsBoardScene(activeScene))
            {
                // 已经在棋盘场景，只需恢复根物体
                SetBoardSceneRootsActive(true);
                // 返回棋盘时重新显示玩家
                if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
                {
                    GameManager.Instance.CurrentPlayer.SetActive(true);
                }
                RaiseBoardModeChanged();
                CompleteTransition(transitionToken);
                return null;
            }

            // 从菜单等场景进入棋盘，直接 Single 加载
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op != null)
            {
                op.completed += _ => CompleteTransition(transitionToken);
            }
            else
            {
                CompleteTransition(transitionToken);
            }
            return op;
        }

        /// <summary>
        /// 获取棋盘场景名（优先使用最后记录的棋盘场景）。
        /// </summary>
        private string GetBoardSceneName()
        {
            return string.IsNullOrEmpty(_lastBoardSceneName) ? boardSceneName : _lastBoardSceneName;
        }

        /// <summary>
        /// 获取棋盘 Scene 对象（优先使用缓存）。
        /// </summary>
        private Scene GetBoardScene()
        {
            if (_boardScene.IsValid() && _boardScene.isLoaded) return _boardScene;
            return SceneManager.GetSceneByName(GetBoardSceneName());
        }

        /// <summary>
        /// 判断给定场景是否为棋盘场景。
        /// </summary>
        private bool IsBoardScene(Scene scene)
        {
            if (!scene.IsValid()) return false;
            if (sceneRegistry != null)
            {
                return sceneRegistry.GetGameMode(scene.name) == GameMode.Board;
            }
            return scene.name == GetBoardSceneName();
        }

        /// <summary>
        /// 将棋盘场景设为 ActiveScene，确保其光照/相机生效。
        /// </summary>
        private void ActivateBoardScene()
        {
            Scene boardScene = GetBoardScene();
            if (boardScene.IsValid() && boardScene.isLoaded)
            {
                SceneManager.SetActiveScene(boardScene);
            }
            else
            {
                DebugTools.LogWarning($"[SceneLoader] Board scene '{GetBoardSceneName()}' is not loaded.");
            }
        }

        /// <summary>
        /// 切换棋盘场景根物体的激活状态：
        /// 用于“隐藏棋盘但保留状态”。
        /// </summary>
        private void SetBoardSceneRootsActive(bool active)
        {
            Scene boardScene = GetBoardScene();
            if (!boardScene.IsValid() || !boardScene.isLoaded) return;
            GameObject[] roots = boardScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null) continue;
                // 不影响 DontDestroyOnLoad 根节点上的全局单例
                if (root.scene.name == "DontDestroyOnLoad") continue;
                root.SetActive(active);
            }
        }

        /// <summary>
        /// 广播“棋盘模式”并同步 GameManager 状态。
        /// </summary>
        private void RaiseBoardModeChanged()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameState.BoardMode);
            }
            Scene boardScene = GetBoardScene();
            EventBus.Raise(new GameModeChangedEvent
            {
                SceneName = boardScene.IsValid() ? boardScene.name : GetBoardSceneName(),
                Mode = GameMode.Board
            });
        }
    }
}
