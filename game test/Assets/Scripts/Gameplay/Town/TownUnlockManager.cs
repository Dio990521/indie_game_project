using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Board.Data;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Town
{
    /// <summary>
    /// 城镇解锁管理器（单例 + 存档）：
    /// 追踪玩家已路过或进入过的 TownTile 节点，提供传送选单所需的数据。
    ///
    /// 设计说明：
    /// - _unlockedNodeIds（HashSet）负责持久化，存档时序列化为 List&lt;int&gt;
    /// - _townTileCache（Dictionary）是纯运行时缓存，每次进入 Board 模式时从
    ///   BoardMapManager 重建，解决 ScriptableObject 引用无法 JSON 序列化的问题
    /// </summary>
    public class TownUnlockManager : SaveableMonoSingleton<TownUnlockManager>
    {
        // ── 存档 ─────────────────────────────────────────────────────────────

        public override string SaveID => "TownUnlockManager";

        [Serializable]
        private class SaveState
        {
            public List<int> UnlockedNodeIds = new List<int>();
        }

        public override object CaptureState()
        {
            return new SaveState { UnlockedNodeIds = new List<int>(_unlockedNodeIds) };
        }

        public override void RestoreState(object data)
        {
            if (data is not SaveState s) return;
            _unlockedNodeIds.Clear();
            foreach (int id in s.UnlockedNodeIds)
                _unlockedNodeIds.Add(id);
            // TownTile 引用由 GameModeChangedEvent(Board) 触发 RebuildTownTileCache() 后延迟解析
        }

        // ── 运行时状态 ────────────────────────────────────────────────────────

        private readonly HashSet<int> _unlockedNodeIds = new HashSet<int>();
        private readonly Dictionary<int, TownTile> _townTileCache = new Dictionary<int, TownTile>();

        // ── 生命周期 ──────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        // ── 公开接口 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 解锁指定 nodeId 对应的城镇（幂等）。
        /// 由 TownTile.OnEnter() 在玩家路过或停留时调用。
        /// </summary>
        public void UnlockTown(int nodeId)
        {
            if (nodeId < 0) return;
            bool isNew = _unlockedNodeIds.Add(nodeId);
            if (isNew)
                DebugTools.Log($"[TownUnlock] 解锁城镇 nodeId={nodeId}");
        }

        /// <summary>
        /// 获取所有已解锁城镇列表，可指定排除某个 nodeId（用于排除当前城镇）。
        /// 返回有 TownTile 缓存的条目；若缓存未就绪则跳过并输出警告。
        /// </summary>
        public List<(int nodeId, TownTile tile)> GetUnlockedTowns(int excludeNodeId = -1)
        {
            var result = new List<(int, TownTile)>(_unlockedNodeIds.Count);
            foreach (int id in _unlockedNodeIds)
            {
                if (id == excludeNodeId) continue;
                if (_townTileCache.TryGetValue(id, out TownTile tile))
                    result.Add((id, tile));
                else
                    DebugTools.LogWarning($"[TownUnlock] nodeId={id} 尚无 TownTile 缓存（地图未就绪？）");
            }
            return result;
        }

        /// <summary>
        /// 查询指定 nodeId 是否已解锁。
        /// </summary>
        public bool IsUnlocked(int nodeId) => _unlockedNodeIds.Contains(nodeId);

        // ── 内部逻辑 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 扫描 BoardMapManager 中所有节点，缓存 nodeId → TownTile 的映射。
        /// 在 Board 模式就绪后调用，确保 BoardMapManager 已完成初始化。
        /// </summary>
        private void RebuildTownTileCache()
        {
            _townTileCache.Clear();
            var allNodes = BoardMapManager.Instance?.GetAllNodes();
            if (allNodes == null)
            {
                DebugTools.LogWarning("[TownUnlock] BoardMapManager 未就绪，无法重建城镇缓存。");
                return;
            }

            int count = 0;
            foreach (var node in allNodes)
            {
                if (node?.tileData is TownTile town)
                {
                    _townTileCache[node.nodeID] = town;
                    count++;
                }
            }
            DebugTools.Log($"[TownUnlock] 城镇缓存重建完成，共 {count} 个城镇节点。");
        }

        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            if (evt.Mode == GameMode.Board)
                RebuildTownTileCache();
        }
    }
}
