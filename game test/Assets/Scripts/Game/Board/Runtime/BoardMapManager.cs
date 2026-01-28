using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime
{
    // 负责缓存棋盘节点索引，提供 O(1) 查询。
    public class BoardMapManager : MonoSingleton<BoardMapManager>
    {
        private readonly Dictionary<int, MapWaypoint> _nodes = new Dictionary<int, MapWaypoint>();
        private bool _isReady;

        public bool IsReady => _isReady;

        private void OnEnable()
        {
            EventBus.Subscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameModeChangedEvent>(HandleGameModeChanged);
        }

        // 由 GameManager 或进入 Board 模式时显式调用。
        public void Init()
        {
            _nodes.Clear();
            MapWaypoint[] waypoints = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            for (int i = 0; i < waypoints.Length; i++)
            {
                MapWaypoint node = waypoints[i];
                if (node == null) continue;
                _nodes[node.nodeID] = node;
            }
            if ( _nodes.Count > 0)
            {
                _isReady = true;
                Debug.Log($"[BoardMapManager] Cached nodes: {_nodes.Count}");
            }
            
        }

        public MapWaypoint GetNode(int id)
        {
            if (!_isReady)
            {
                Init();
            }
            _nodes.TryGetValue(id, out MapWaypoint node);
            return node;
        }

        public List<MapWaypoint> GetAllNodes()
        {
            if (!_isReady)
            {
                Init();
            }
            return new List<MapWaypoint>(_nodes.Values);
        }

        private void HandleGameModeChanged(GameModeChangedEvent evt)
        {
            if (evt.Mode != GameMode.Board) return;
            Init();
        }
    }
}
