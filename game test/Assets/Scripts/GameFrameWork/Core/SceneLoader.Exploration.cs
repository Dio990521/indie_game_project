using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using IndieGame.Core.Utilities;
using IndieGame.UI;

namespace IndieGame.Core
{
    /// <summary>
    /// SceneLoader 的探索场景策略 partial：
    /// 包含"棋盘常驻 + Additive 叠加探索"的加载/卸载链——
    /// 从菜单/棋盘/其他探索场景进入探索的三种路径，以及 Additive 加载与 ActiveScene 切换。
    /// 主流程入口与转场 Token 管理见 SceneLoader.cs，棋盘策略见 SceneLoader.Board.cs。
    /// </summary>
    public partial class SceneLoader
    {
        /// <summary>
        /// 加载探索场景：
        /// - 若当前是菜单：先 Single 加载棋盘，再 Additive 叠加探索
        /// - 若当前是探索：先卸载旧探索，再叠加新探索
        /// - 若当前是棋盘：隐藏棋盘根物体，再叠加探索
        /// </summary>
        private AsyncOperation LoadExplorationScene(string sceneName, int transitionToken)
        {
            GameMode activeMode = GetModeForScene(SceneManager.GetActiveScene().name);
            if (activeMode == GameMode.Title)
            {
                AsyncOperation loadBoardOp = SceneManager.LoadSceneAsync(GetBoardSceneName(), LoadSceneMode.Single);
                if (loadBoardOp != null)
                {
                    loadBoardOp.completed += _ =>
                    {
                        // M1 修复：过期转场的回调不再执行副作用
                        if (IsStaleTransition(transitionToken)) return;

                        // 先让棋盘常驻，再叠加探索
                        _boardScene = SceneManager.GetSceneByName(GetBoardSceneName());
                        SetBoardSceneRootsActive(false);
                        LoadExplorationAdditive(sceneName, transitionToken);
                    };
                }
                return loadBoardOp;
            }

            if ((activeMode == GameMode.Exploration || activeMode == GameMode.Camp || activeMode == GameMode.Combat) && !string.IsNullOrEmpty(_currentExplorationScene))
            {
                Scene currentExploration = SceneManager.GetSceneByName(_currentExplorationScene);
                if (currentExploration.IsValid() && currentExploration.isLoaded && _currentExplorationScene != sceneName)
                {
                    AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(currentExploration);
                    if (unloadOp != null)
                    {
                        // 卸载旧探索后再加载新探索（M1 修复：过期转场不再续接加载）
                        unloadOp.completed += _ =>
                        {
                            if (IsStaleTransition(transitionToken)) return;
                            LoadExplorationAdditive(sceneName, transitionToken);
                        };
                    }
                    return unloadOp;
                }
            }

            if (IsBoardScene(SceneManager.GetActiveScene()))
            {
                // 从棋盘进入探索时先隐藏棋盘根物体
                SetBoardSceneRootsActive(false);
            }

            return LoadExplorationAdditive(sceneName, transitionToken);
        }

        /// <summary>
        /// 以 Additive 方式叠加探索场景，并设置为 ActiveScene。
        /// </summary>
        private AsyncOperation LoadExplorationAdditive(string sceneName, int transitionToken)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null)
            {
                op.completed += _ =>
                {
                    // M1 修复：过期转场不再执行 SetActiveScene 等副作用
                    if (IsStaleTransition(transitionToken)) return;

                    Scene loadedScene = SceneManager.GetSceneByName(sceneName);
                    if (loadedScene.IsValid() && loadedScene.isLoaded)
                    {
                        // 切换活动场景，确保灯光/摄像机等生效
                        SceneManager.SetActiveScene(loadedScene);
                        _currentExplorationScene = sceneName;
                        CompleteTransition(transitionToken);
                    }
                    else
                    {
                        DebugTools.LogWarning($"[SceneLoader] Additive scene '{sceneName}' did not load correctly.");
                        CompleteTransition(transitionToken);
                    }
                };
            }
            else
            {
                CompleteTransition(transitionToken);
            }
            if (GameManager.Instance != null)
            {
                // 按目标场景在注册表中的模式同步游戏状态：
                // 战斗场景进入 Combat，其余叠加场景（探索/露营）保持原有 FreeRoam 行为
                GameMode targetMode = GetModeForScene(sceneName);
                GameManager.Instance.ChangeState(
                    targetMode == GameMode.Combat ? GameState.Combat : GameState.FreeRoam);
            }
            return op;
        }
    }
}
