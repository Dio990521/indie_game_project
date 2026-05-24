using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Gameplay.Board.Data;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 大世界格子随机刷新管理器。
    /// 挂载在大世界棋盘场景的对象上，监听 DateChangedEvent，
    /// 在每次推进到新的一天时按配置随机重新布局可刷新节点。
    ///
    /// 可刷新节点的判定规则：
    ///   1. MapWaypoint.fixedLayout == false
    ///   2. tileData == null 或 tileData.AlwaysFixed == false
    ///      （WarpTile / TeleportTile / TownTile 的 AlwaysFixed 均为 true，始终锁定）
    /// </summary>
    public class BoardRefreshManager : SaveableBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector 字段
        // ------------------------------------------------------------------

        [Header("随机刷新主开关")]
        [Tooltip("此开关 AND BoardRefreshConfigSO.enableRandomRefresh 均为 true 时，刷新才会执行")]
        [SerializeField] private bool _enableRandomRefresh = true;

        [Header("配置文件")]
        [SerializeField] private BoardRefreshConfigSO _config;

        // ------------------------------------------------------------------
        // 运行时状态
        // ------------------------------------------------------------------

        /// <summary> 已参与上次刷新的节点分配结果，供存档系统序列化。 </summary>
        private readonly List<NodeAssignment> _lastAssignments = new();

        // ------------------------------------------------------------------
        // 存档 ID
        // ------------------------------------------------------------------

        public override string SaveID => "BoardRefreshManager";

        // ------------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------------

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<DateChangedEvent>(OnDayAdvanced);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<DateChangedEvent>(OnDayAdvanced);
        }

        // ------------------------------------------------------------------
        // 初始刷新
        // ------------------------------------------------------------------

        private void Start()
        {
            StartCoroutine(InitialRefreshIfNeeded());
        }

        /// <summary>
        /// 等一帧后检查：若 RestoreState 没有带来任何存档数据（全新游戏），则主动执行一次初始刷新。
        /// </summary>
        private IEnumerator InitialRefreshIfNeeded()
        {
            yield return null; // 让 SaveManager 的 RestoreState 有机会先执行
            if (!_enableRandomRefresh) yield break;
            if (_config == null || !_config.enableRandomRefresh) yield break;
            if (_lastAssignments.Count == 0)
                RefreshBoard();
        }

        // ------------------------------------------------------------------
        // 刷新触发
        // ------------------------------------------------------------------

        private void OnDayAdvanced(DateChangedEvent _)
        {
            if (!_enableRandomRefresh) return;
            if (_config == null || !_config.enableRandomRefresh) return;
            RefreshBoard();
        }

        // ------------------------------------------------------------------
        // 核心刷新算法
        // ------------------------------------------------------------------

        /// <summary>
        /// 执行一次完整的随机刷新。
        /// </summary>
        public void RefreshBoard()
        {
            var refreshable = CollectRefreshableNodes();
            if (refreshable.Count == 0) return;

            _lastAssignments.Clear();

            foreach (var node in refreshable)
            {
                int idx = _config.PickRandomTileIndex();
                node.tileData = _config.GetTileByIndex(idx);
                _lastAssignments.Add(new NodeAssignment { nodeId = node.nodeID, poolIndex = idx });
            }
        }

        // ------------------------------------------------------------------
        // 工具方法
        // ------------------------------------------------------------------

        private List<MapWaypoint> CollectRefreshableNodes()
        {
            var all = BoardMapManager.Instance.GetAllNodes();
            var result = new List<MapWaypoint>(all.Count);
            foreach (var node in all)
            {
                if (!node.fixedLayout &&
                    (node.tileData == null || !node.tileData.AlwaysFixed))
                    result.Add(node);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // 存档系统（ISaveable）
        // ------------------------------------------------------------------

        public override object CaptureState()
        {
            return new RefreshSaveData { assignments = new List<NodeAssignment>(_lastAssignments) };
        }

        public override void RestoreState(object data)
        {
            if (data is not RefreshSaveData saved) return;
            if (saved.assignments == null || saved.assignments.Count == 0) return;

            _lastAssignments.Clear();
            var mapManager = BoardMapManager.Instance;

            foreach (var a in saved.assignments)
            {
                var node = mapManager.GetNode(a.nodeId);
                if (node == null) continue;

                node.tileData = _config != null ? _config.GetTileByIndex(a.poolIndex) : null;
                _lastAssignments.Add(a);
            }
        }

        // ------------------------------------------------------------------
        // 统计查询（供 Editor 展示）
        // ------------------------------------------------------------------

        public struct TileStat
        {
            public TileBase tile;   // null = 空格
            public string label;    // 显示名称
            public int count;
            public float percentage;
        }

        /// <summary>
        /// 返回上次刷新的格子分布统计，仅在运行时有数据。
        /// </summary>
        public List<TileStat> GetCurrentStats()
        {
            var result = new List<TileStat>();
            if (_lastAssignments.Count == 0) return result;

            // 按 poolIndex 聚合计数
            var counts = new Dictionary<int, int>();
            foreach (var a in _lastAssignments)
            {
                counts.TryGetValue(a.poolIndex, out int c);
                counts[a.poolIndex] = c + 1;
            }

            int total = _lastAssignments.Count;
            foreach (var kv in counts)
            {
                var tile = _config != null ? _config.GetTileByIndex(kv.Key) : null;
                result.Add(new TileStat
                {
                    tile       = tile,
                    label      = tile != null ? tile.tileName : "空格",
                    count      = kv.Value,
                    percentage = (float)kv.Value / total
                });
            }

            // 按数量降序排列，方便阅读
            result.Sort((a, b) => b.count.CompareTo(a.count));
            return result;
        }

        // ------------------------------------------------------------------
        // 存档数据结构
        // ------------------------------------------------------------------

        [Serializable]
        private class RefreshSaveData
        {
            public List<NodeAssignment> assignments;
        }

        [Serializable]
        private class NodeAssignment
        {
            public int nodeId;
            public int poolIndex; // -1 = 空格
        }
    }
}
