using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.CameraSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.Data;
using IndieGame.Gameplay.Board.FogOfWar;
using IndieGame.Gameplay.Treasure;
using IndieGame.UI;
using IndieGame.UI.Treasure;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 斗篷宝具激活状态：
    /// 复用宝具菜单（TreasureMenuView.ShowSimple）展示所有已探查的普通格列表，
    /// 玩家选择后传送到该格，不触发格子效果。
    /// 确认时菜单发布 TreasureItemSelectedEvent（Id = nodeID 字符串），取消时发布 TreasureMenuCancelledEvent。
    /// </summary>
    public class CloakTreasureState : BoardState
    {
        private readonly CloakTreasureSO _data;
        private Coroutine _routine;

        // 协程轮询标志
        private bool _nodeSelected;
        private bool _cancelled;
        private int  _selectedNodeId;

        // 字段级委托：StopCoroutine 不执行 finally，OnExit 必须在此处保证注销
        private System.Action<TreasureItemSelectedEvent>   _onSelectedDelegate;
        private System.Action<TreasureMenuCancelledEvent>  _onCancelledDelegate;

        public CloakTreasureState(CloakTreasureSO data)
        {
            _data = data;
        }

        // ── 状态生命周期 ──────────────────────────────────────────────────

        public override void OnEnter(BoardGameManager context)
        {
            if (_data == null ||
                ActionPointSystem.Instance == null ||
                !ActionPointSystem.Instance.CanConsume(_data.ActionPointCost))
            {
                DebugTools.Log("[CloakTreasureState] 行动力不足或数据缺失，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                return;
            }

            _nodeSelected = false;
            _cancelled    = false;
            _routine = context.StartCoroutine(CloakRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }

            // StopCoroutine 不执行协程后续代码，字段级委托必须在此处保证注销
            if (_onSelectedDelegate != null)
            {
                EventBus.Unsubscribe(_onSelectedDelegate);
                _onSelectedDelegate = null;
            }
            if (_onCancelledDelegate != null)
            {
                EventBus.Unsubscribe(_onCancelledDelegate);
                _onCancelledDelegate = null;
            }

            // 兜底：若 UI 因意外中断未关闭，强制隐藏
            GetTreasureMenu()?.Hide();
        }

        // ── 协程主体 ──────────────────────────────────────────────────────

        private IEnumerator CloakRoutine(BoardGameManager context)
        {
            // 1. 收集候选节点：已揭开的普通格
            List<MapWaypoint> candidates = CollectRevealedNormalNodes();

            if (candidates.Count == 0)
            {
                DebugTools.Log("[CloakTreasureState] 没有已探查的普通格，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 2. 获取菜单实例
            TreasureMenuView menu = GetTreasureMenu();
            if (menu == null)
            {
                DebugTools.LogWarning("[CloakTreasureState] TreasureMenuInstance 未配置，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 3. 构建简单条目列表（Id = nodeID 字符串，调用方解析）
            var items = new List<SimpleMenuItem>(candidates.Count);
            foreach (MapWaypoint node in candidates)
            {
                Vector3 pos = node.transform.position;
                items.Add(new SimpleMenuItem
                {
                    Id          = node.nodeID.ToString(),
                    DisplayText = $"#{node.nodeID}  ({pos.x:F1}, {pos.z:F1})"
                });
            }

            // 4. 等一帧：确保上一状态的确认键松开事件（InputInteractCanceledEvent）不被菜单误捕获
            //    ShowSimple 内部会调用 SubscribeInput 订阅 InputInteractCanceledEvent，
            //    若在松开前订阅，该键松开会被视为"取消"，与 WingTreasureState 同类问题。
            yield return null;

            // 5. 注册事件（字段级委托，OnExit 保证清理）
            _onSelectedDelegate  = evt =>
            {
                // 仅处理能解析为整数的 Id（过滤掉宝具的字符串 Id，理论上不会出现冲突）
                if (int.TryParse(evt.TreasureId, out int nodeId))
                {
                    _selectedNodeId = nodeId;
                    _nodeSelected   = true;
                }
            };
            _onCancelledDelegate = _ => { _cancelled = true; };
            EventBus.Subscribe(_onSelectedDelegate);
            EventBus.Subscribe(_onCancelledDelegate);

            // 6. 展示菜单（复用宝具菜单）
            menu.ShowSimple(items);

            // 7. 等待玩家操作
            while (!_nodeSelected && !_cancelled)
                yield return null;

            // 8. 正常流程注销事件（OnExit 是双重保险）
            EventBus.Unsubscribe(_onSelectedDelegate);
            _onSelectedDelegate = null;
            EventBus.Unsubscribe(_onCancelledDelegate);
            _onCancelledDelegate = null;

            if (_cancelled)
            {
                DebugTools.Log("[CloakTreasureState] 玩家取消传送，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 9. 消耗行动力
            if (!ActionPointSystem.Instance.TryConsumeActionPoints(_data.ActionPointCost, "CloakTreasure"))
            {
                DebugTools.Log("[CloakTreasureState] 行动力消耗失败，返回玩家回合。");
                context.ChangeState(new PlayerTurnState());
                yield break;
            }

            // 10. 传送（不调用 HandleExternalArrival，不触发格子效果）
            context.movementController.SetCurrentNodeById(_selectedNodeId);

            // 11. 摄像机 Warp（与 BoardGameManager.SyncCameraToPlayer 逻辑一致）
            if (CameraManager.Instance != null && GameManager.Instance?.CurrentPlayer != null)
            {
                CameraManager.Instance.SetFollowTarget(GameManager.Instance.CurrentPlayer.transform);
                CameraManager.Instance.WarpCameraToTarget();
            }

            DebugTools.Log($"[CloakTreasureState] 传送完成，目标节点 ID: {_selectedNodeId}。");

            // 12. 返回玩家回合
            context.ChangeState(new PlayerTurnState());
        }

        // ── 工具方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 收集所有"已被迷雾揭开的普通格（NormalTile）"节点，按 nodeID 升序排列。
        /// 若迷雾系统未启用，视为全部已揭开。
        /// </summary>
        private List<MapWaypoint> CollectRevealedNormalNodes()
        {
            var result = new List<MapWaypoint>();

            List<MapWaypoint> allNodes = BoardMapManager.Instance?.GetAllNodes();
            if (allNodes == null) return result;

            FogOfWarManager fog = FogOfWarManager.Instance;

            foreach (MapWaypoint node in allNodes)
            {
                if (node == null) continue;
                if (!(node.tileData is NormalTile)) continue;
                if (fog != null && !fog.IsRevealedAt(node.transform.position)) continue;
                result.Add(node);
            }

            result.Sort((a, b) => a.nodeID.CompareTo(b.nodeID));
            return result;
        }

        private static TreasureMenuView GetTreasureMenu()
        {
            return UIManager.Instance?.TreasureMenuInstance;
        }
    }
}
