using System.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;
using IndieGame.Core.Input;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Player;
using IndieGame.Gameplay.Treasure;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 木头人宝具激活状态：
    /// 在玩家当前格子位置召唤木头人，允许其在配置半径内自由移动。
    /// 操控期间摄像机切换跟随木头人；玩家按 ESC 摧毁木头人并返回 PlayerTurnState。
    /// 与 WingTreasureState 结构一致，采用协程驱动 + 字段级 ESC 委托（StopCoroutine 安全）。
    /// </summary>
    public class WoodenPuppetTreasureState : BoardState
    {
        private readonly WoodenPuppetTreasureSO _data;

        private Coroutine _routine;
        private GameObject _puppet;
        private bool _exitRequested;

        // ⚠ 委托提升为字段：OnExit 在 StopCoroutine 后仍能安全注销，
        //   因为 Unity 的 StopCoroutine 不执行 C# 迭代器的 finally 块。
        private System.Action _onCancelDelegate;
        private GameInputReader _cachedInputReader;

        public WoodenPuppetTreasureState(WoodenPuppetTreasureSO data)
        {
            _data = data;
        }

        // ══ 状态生命周期 ════════════════════════════════════════════════════

        public override void OnEnter(BoardGameManager context)
        {
            // 行动力二次检查（防止极端情况下数值被其他系统提前消耗）
            if (_data == null ||
                ActionPointSystem.Instance == null ||
                !ActionPointSystem.Instance.CanConsume(_data.ActionPointCost))
            {
                DebugTools.Log("[WoodenPuppetTreasureState] 行动力不足或数据缺失，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            BoardEntity entity = context.movementController?.PlayerEntity;
            if (entity == null || entity.CurrentNode == null)
            {
                DebugTools.LogWarning("[WoodenPuppetTreasureState] 找不到玩家实体或当前节点，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _exitRequested = false;
            _routine = context.StartCoroutine(PuppetRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }

            // 用字段级委托安全注销，无论协程是否被中途 StopCoroutine 打断
            if (_onCancelDelegate != null && _cachedInputReader != null)
            {
                _cachedInputReader.UICancelEvent -= _onCancelDelegate;
            }
            _onCancelDelegate  = null;
            _cachedInputReader = null;

            DestroyPuppet();
            RestoreCamera();
        }

        // ══ 协程主体 ════════════════════════════════════════════════════════

        private IEnumerator PuppetRoutine(BoardGameManager context)
        {
            // 消耗行动力（在协程内确认消耗，确保成功进入流程后才扣除）
            if (!ActionPointSystem.Instance.TryConsumeActionPoints(
                    _data.ActionPointCost, "WoodenPuppetTreasure"))
            {
                DebugTools.Log("[WoodenPuppetTreasureState] 行动力消耗失败，取消召唤。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 获取 InputReader（复用 forkSelector 上挂载的引用，无需额外配置）
            GameInputReader inputReader = context.movementController.forkSelector?.inputReader;
            if (inputReader == null)
            {
                DebugTools.LogWarning("[WoodenPuppetTreasureState] 找不到 GameInputReader，终止召唤。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }
            _cachedInputReader = inputReader;

            // ── 召唤木头人 ──
            BoardEntity entity = context.movementController.PlayerEntity;
            Vector3 spawnPos   = entity.CurrentNode.transform.position;
            _puppet            = SpawnPuppet(spawnPos);

            // 运行时附加控制器并注入参数
            WoodenPuppetController puppetCtrl = _puppet.AddComponent<WoodenPuppetController>();
            puppetCtrl.Init(spawnPos, _data.MaxRadius, _data.MoveSpeed, _data.RotateSpeed);

            // ── 摄像机切换：跟随木头人 ──
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetFollowTarget(_puppet.transform);
            }

            // 等一帧：防止宝具菜单的确认键在同帧或次帧触发 ESC 回调
            // （与 WingTreasureState 第 189 行的保护模式完全一致）
            yield return null;

            // ── 注册 ESC 退出回调（字段级，OnExit 可安全 -= ）──
            _onCancelDelegate = () => _exitRequested = true;
            _cachedInputReader.UICancelEvent += _onCancelDelegate;

            DebugTools.Log("[WoodenPuppetTreasureState] 木头人已召唤，按 ESC 摧毁。");

            // ── 等待玩家按 ESC ──
            while (!_exitRequested)
                yield return null;

            // 正常退出时在协程内注销（OnExit 的注销作为双重保险）
            _cachedInputReader.UICancelEvent -= _onCancelDelegate;
            _onCancelDelegate  = null;
            _cachedInputReader = null;

            // ChangeState 会触发 OnExit（销毁木头人 + 恢复摄像机），再进入 PlayerTurnState
            context.ChangeState(new PlayerTurnState());
        }

        // ══ 工具方法 ════════════════════════════════════════════════════════

        /// <summary>
        /// 实例化木头人：优先使用 SO 配置的预制体，否则创建默认 Cube（测试兜底）。
        /// </summary>
        private GameObject SpawnPuppet(Vector3 position)
        {
            if (_data.PuppetPrefab != null)
            {
                return Object.Instantiate(_data.PuppetPrefab, position, Quaternion.identity);
            }

            // 默认 Cube：移除 BoxCollider 避免影响棋盘物理层
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = position;
            cube.name = "WoodenPuppet_Default";
            Object.Destroy(cube.GetComponent<BoxCollider>());
            return cube;
        }

        /// <summary>安全销毁木头人 GameObject。</summary>
        private void DestroyPuppet()
        {
            if (_puppet == null) return;
            Object.Destroy(_puppet);
            _puppet = null;
        }

        /// <summary>
        /// 将摄像机跟随目标恢复为玩家，让镜头平滑滑回（不调用 WarpCameraToTarget）。
        /// </summary>
        private void RestoreCamera()
        {
            if (CameraManager.Instance == null) return;
            if (GameManager.Instance?.CurrentPlayer == null) return;

            CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
        }
    }
}
