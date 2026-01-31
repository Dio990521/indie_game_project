using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;

namespace IndieGame.Gameplay.Exploration
{
    /// <summary>
    /// 场景初始化器：
    /// 专门负责非棋盘场景（如城镇、探索区）的启动逻辑。
    /// 它的核心职责是解析加载数据，将玩家瞬间移动到正确的出生点 (SpawnPoint)。
    /// </summary>
    public class SceneInitializer : MonoBehaviour
    {
        private void Start()
        {
            // 1. 获取全局场景加载器引用
            SceneLoader loader = SceneLoader.Instance;

            // 检查初始化前提：
            // - loader 必须存在
            // - loader 必须携带有效载荷 (HasPayload)，意味着这是有计划的跳转
            // - 如果当前是“返回棋盘”的过程 (IsReturnToBoard)，则不由此脚本处理，应由 BoardGameManager 负责
            if (loader == null || !loader.HasPayload || loader.IsReturnToBoard) return;

            // 2. 获取目标位置 ID（例如：从大地图进入城镇 A，target 则是城镇 A 的 ID）
            LocationID target = loader.TargetLocationId;
            if (target == null)
            {
                // 如果没有指定位置，清理载荷并退出，防止下次加载出错
                loader.ClearPayload();
                return;
            }

            // 3. 寻找出生点：
            // 在当前场景的所有 SpawnPoint 中，寻找 ID 与 target 匹配的那一个
            if (!SpawnPointRegistry.TryGet(target, out SpawnPoint spawn))
            {
                Debug.LogWarning("[SceneInitializer] 未能在场景中找到匹配该 LocationID 的 SpawnPoint。");
                loader.ClearPayload();
                return;
            }

            // 4. 准备玩家对象：
            // 确保全局管理器和玩家对象已经生成并就绪
            if (GameManager.Instance == null || GameManager.Instance.CurrentPlayer == null)
            {
                Debug.LogWarning("[SceneInitializer] 玩家对象尚未就绪。");
                loader.ClearPayload();
                return;
            }

            // 5. 执行传送：
            // 将玩家的位置设置为对应出生点的位置
            GameObject player = GameManager.Instance.CurrentPlayer;
            player.transform.position = spawn.transform.position;

            // 6. 相机同步：
            // 确保相机立即跟随传送后的玩家，而不是从上一个位置缓慢滑行过来
            if (CameraManager.Instance != null)
            {
                // 设置相机的追踪目标
                CameraManager.Instance.SetFollowTarget(player.transform);
                // 瞬间将相机“瞬移”到目标点（Warp），消除由于距离过远产生的平滑过渡干扰
                CameraManager.Instance.WarpCameraToTarget();
            }

            // 7. 完成清理：
            // 重置加载器的载荷状态，防止意外重复触发初始化逻辑
            loader.ClearPayload();
        }
    }
}
