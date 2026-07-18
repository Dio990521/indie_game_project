using System.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗场景启动引导（挂在战斗场景内）：
    /// 统一两条进入路径的启动入口——
    /// 1) 正常链路：棋盘等入口已写入 CombatLaunchContext 载荷 → 直接启动战斗；
    /// 2) 独立测试：编辑器直接 Play 战斗场景（无载荷）→ 用兜底遭遇写入测试载荷、
    ///    隐藏常驻探索玩家、对齐 GameState 后启动战斗。
    /// 场景内需同时存在 GameBootstrapper（isTestScene=false）保证常驻管理器就绪。
    /// </summary>
    public class CombatTestBootstrapper : MonoBehaviour
    {
        [Tooltip("独立测试用的兜底遭遇配置（正常链路带载荷进入时不使用）")]
        [SerializeField] private EncounterSO fallbackEncounter;

        [Tooltip("等待常驻管理器就绪的超时秒数")]
        [SerializeField] private float managerWaitTimeout = 10f;

        private IEnumerator Start()
        {
            // 1) 等待 GameBootstrapper 完成全局初始化
            float deadline = Time.realtimeSinceStartup + managerWaitTimeout;
            while ((GameManager.Instance == null || !GameManager.Instance.IsInitialized)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (GameManager.Instance == null || !GameManager.Instance.IsInitialized)
            {
                DebugTools.LogError("[CombatTestBootstrapper] 等待 GameManager 初始化超时，战斗无法启动。" +
                                    "请确认战斗场景内有 GameBootstrapper。");
                yield break;
            }

            // 2) 无载荷 = 独立测试模式：写入兜底载荷并对齐环境
            if (!CombatLaunchContext.HasPending)
            {
                if (fallbackEncounter == null)
                {
                    DebugTools.LogError("[CombatTestBootstrapper] 独立测试缺少兜底遭遇配置（fallbackEncounter）。");
                    yield break;
                }
                CombatLaunchContext.SetStandaloneTest(fallbackEncounter);

                // 隐藏常驻探索玩家（正常链路由 SceneLoader.HandleSceneLoaded 处理）
                if (GameManager.Instance.CurrentPlayer != null)
                {
                    GameManager.Instance.CurrentPlayer.SetActive(false);
                }

                // 兜底对齐游戏状态（SceneRegistry 已配置 Combat 时 SceneLoader.Init 会先行广播，这里是双保险）
                if (GameManager.Instance.CurrentState != GameState.Combat)
                {
                    GameManager.Instance.ChangeState(GameState.Combat);
                }
                DebugTools.Log("<color=orange>[CombatTestBootstrapper] 独立测试模式启动。</color>");
            }

            // 3) 等待场景内 CombatManager 就绪后启动战斗
            while (CombatManager.Instance == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (CombatManager.Instance == null)
            {
                DebugTools.LogError("[CombatTestBootstrapper] 未找到 CombatManager，战斗无法启动。");
                yield break;
            }
            CombatManager.Instance.StartBattle();
        }
    }
}
