using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Gameplay.Board.Data;
using IndieGame.Gameplay.Date;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 大世界格子随机刷新管理器。
    ///
    /// 触发时机：每次进入棋盘场景（GameModeChangedEvent Board）时，
    /// 直接查询 DateSystem.Instance 获取当前日期，
    /// 与存档中记录的上次刷新日期对比，不同则刷新。
    ///
    /// 不依赖 DateChangedEvent 触发刷新——该事件在营地/旅馆场景发出时
    /// 棋盘场景已卸载，Manager 根本收不到。
    /// </summary>
    public class BoardRefreshManager : SaveableBehaviour
    {
        [Header("随机刷新主开关")]
        [Tooltip("此开关 AND BoardRefreshConfigSO.enableRandomRefresh 均为 true 时才会刷新")]
        [SerializeField] private bool _enableRandomRefresh = true;

        [Header("配置文件")]
        [SerializeField] private BoardRefreshConfigSO _config;

        // ------------------------------------------------------------------
        // 运行时状态
        // ------------------------------------------------------------------

        private readonly List<NodeAssignment> _lastAssignments = new();

        /// <summary> 上次执行刷新时的日期字符串（如"第1年1月2日"）。空字符串表示从未刷新。 </summary>
        private string _lastRefreshDate = string.Empty;

        // 用 GameObject 名作为区分，允许多个地区各自独立存档
        public override string SaveID => $"BoardRefreshManager_{gameObject.name}";

        // ------------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------------

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
        }

        // ------------------------------------------------------------------
        // 触发逻辑
        // ------------------------------------------------------------------

        private void OnGameModeChanged(GameModeChangedEvent evt)
        {
            if (evt.Mode != GameMode.Board) return;
            if (!_enableRandomRefresh) return;
            if (_config == null || !_config.enableRandomRefresh) return;
            StartCoroutine(CheckAndRefreshRoutine());
        }

        private IEnumerator CheckAndRefreshRoutine()
        {
            // 等一帧，确保 BoardMapManager.Init() 已完成
            yield return null;

            string currentDate  = DateSystem.Instance != null ? DateSystem.Instance.GetFormattedDate() : string.Empty;
            bool neverRefreshed = _lastAssignments.Count == 0;
            bool dateChanged    = currentDate != _lastRefreshDate;

            if (neverRefreshed || dateChanged)
                RefreshBoard();
        }

        // ------------------------------------------------------------------
        // 刷新
        // ------------------------------------------------------------------

        public event Action OnRefreshCompleted;

        [ContextMenu("手动触发刷新")]
        public void ManualRefresh() => RefreshBoard();

        public void RefreshBoard()
        {
            var refreshable = CollectRefreshableNodes();
            if (refreshable.Count == 0)
            {
                Debug.LogWarning("[BoardRefreshManager] 没有可刷新节点。");
                return;
            }

            _lastAssignments.Clear();

            // 两阶段约束分配：先满足 minCount，再权重随机填充剩余（不超过 maxCount）
            var assignments = _config.AllocateTiles(refreshable.Count);
            for (int i = 0; i < refreshable.Count; i++)
            {
                int idx = i < assignments.Count ? assignments[i] : -1;
                refreshable[i].tileData = _config.GetTileByIndex(idx);
                _lastAssignments.Add(new NodeAssignment { nodeId = refreshable[i].nodeID, poolIndex = idx });
            }

            _lastRefreshDate = DateSystem.Instance != null
                ? DateSystem.Instance.GetFormattedDate()
                : string.Empty;

            OnRefreshCompleted?.Invoke();
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------

        private List<MapWaypoint> CollectRefreshableNodes()
        {
            // 只收集挂在此 GameObject 下的子节点，实现各地区独立刷新
            var all    = GetComponentsInChildren<MapWaypoint>();
            var result = new List<MapWaypoint>(all.Length);
            foreach (var node in all)
            {
                if (!node.fixedLayout &&
                    (node.tileData == null || !node.tileData.AlwaysFixed))
                    result.Add(node);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // 统计（供 Editor）
        // ------------------------------------------------------------------

        public struct TileStat
        {
            public TileBase tile;
            public string   label;
            public int      count;
            public float    percentage;
        }

        public List<TileStat> GetCurrentStats()
        {
            var result = new List<TileStat>();
            if (_lastAssignments.Count == 0) return result;

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
            result.Sort((a, b) => b.count.CompareTo(a.count));
            return result;
        }

        // ------------------------------------------------------------------
        // 存档
        // ------------------------------------------------------------------

        public override object CaptureState() =>
            new RefreshSaveData
            {
                assignments     = new List<NodeAssignment>(_lastAssignments),
                lastRefreshDate = _lastRefreshDate
            };

        public override void RestoreState(object data)
        {
            if (data is not RefreshSaveData saved) return;

            _lastRefreshDate = saved.lastRefreshDate ?? string.Empty;
            _lastAssignments.Clear();

            if (saved.assignments == null || saved.assignments.Count == 0) return;

            var map = BoardMapManager.Instance;
            foreach (var a in saved.assignments)
            {
                var node = map.GetNode(a.nodeId);
                if (node == null) continue;
                node.tileData = _config != null ? _config.GetTileByIndex(a.poolIndex) : null;
                _lastAssignments.Add(a);
            }
        }

        // ------------------------------------------------------------------
        // 数据结构
        // ------------------------------------------------------------------

        [Serializable]
        private class RefreshSaveData
        {
            public List<NodeAssignment> assignments;
            public string               lastRefreshDate;
        }

        [Serializable]
        private class NodeAssignment
        {
            public int nodeId;
            public int poolIndex;
        }
    }
}
