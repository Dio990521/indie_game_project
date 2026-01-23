using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Events;
using IndieGame.Gameplay.Board.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IndieGame.Gameplay.Board.Runtime
{
    [System.Serializable]
    public class WaypointConnection
    {
        public MapWaypoint targetNode;
        public Vector3 controlPointOffset;

        [Header("Connection Events")]
        [Tooltip("在这条连线上触发的事件列表")]
        public List<ConnectionEvent> events = new List<ConnectionEvent>();
    }

    [System.Serializable]
    public class ConnectionEvent
    {
        [Tooltip("触发点在路径上的比例 (0 = 起点, 1 = 终点)")]
        [Range(0.01f, 0.99f)] 
        public float progressPoint = 0.5f;

        [Header("Architecture: Command Pattern")]
        [Tooltip("拖入一个具体的事件配置文件 (ScriptableObject)")]
        public BoardEventSO eventAction;

        [Tooltip("事件执行时的参考目标（可选，比如看向的物体，或者特效生成的位置）")]
        public Transform contextTarget;
        
        // 运行时标记，防止重复触发
        [HideInInspector] public bool hasTriggered = false; 
    }

    public class MapWaypoint : MonoBehaviour
    {
        [Header("ID Settings")]
        [Tooltip("每个节点的唯一编号，用于自动连接")]
        public int nodeID = 0;

        [Header("Data")]
        public TileBase tileData;

        [Header("Connections")]
        public List<WaypointConnection> connections = new List<WaypointConnection>();

        private void Awake()
        {
            // [优化] 预处理：在游戏开始时就将所有连接上的事件按进度排序
            // 这样运行时就不需要用 LinQ 的 OrderBy 了，极大节省性能
            foreach (var conn in connections)
            {
                if (conn.events != null && conn.events.Count > 1)
                {
                    conn.events.Sort((a, b) => a.progressPoint.CompareTo(b.progressPoint));
                }
            }
        }

        public List<MapWaypoint> GetValidNextNodes(MapWaypoint incomingFrom)
        {
            List<MapWaypoint> results = new List<MapWaypoint>();
            for (int i = 0; i < connections.Count; i++)
            {
                MapWaypoint target = connections[i].targetNode;
                if (target == null || target == incomingFrom) continue;
                results.Add(target);
            }

            if (results.Count == 0 && incomingFrom != null)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    MapWaypoint target = connections[i].targetNode;
                    if (target == incomingFrom)
                    {
                        results.Add(target);
                        break;
                    }
                }
            }

            return results;
        }

        public WaypointConnection GetConnectionTo(MapWaypoint node)
        {
            if (node == null) return null;
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].targetNode == node) return connections[i];
            }
            return null;
        }

        public List<WaypointConnection> GetConnectionsTo(List<MapWaypoint> nodes)
        {
            List<WaypointConnection> results = new List<WaypointConnection>();
            if (nodes == null || nodes.Count == 0) return results;

            HashSet<MapWaypoint> lookup = new HashSet<MapWaypoint>(nodes);
            for (int i = 0; i < connections.Count; i++)
            {
                MapWaypoint target = connections[i].targetNode;
                if (target != null && lookup.Contains(target)) results.Add(connections[i]);
            }
            return results;
        }

        private void OnDrawGizmos()
        {
            // 在 Scene 窗口画出球体
            if (tileData != null)
            {
                Gizmos.color = tileData.gizmoColor;
                Gizmos.DrawSphere(transform.position, 0.4f);
            }
            else
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position, 0.4f);
            }

#if UNITY_EDITOR
            // 在球体上方显示 ID，方便调试
            Handles.Label(transform.position + Vector3.up * 0.8f, $"ID: {nodeID}", new GUIStyle() { 
                fontSize = 15, 
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState() { textColor = Color.white } 
            });
#endif
        }
    }
}
