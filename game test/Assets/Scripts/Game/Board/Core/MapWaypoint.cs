using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Events;
using IndieGame.Gameplay.Board.Data;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 路点连接定义：描述从当前节点指向另一个节点的路径。
    /// 包含路径形状控制和路径上的中间事件。
    /// </summary>
    [System.Serializable]
    public class WaypointConnection
    {
        [Tooltip("该路径指向的目标节点")]
        public MapWaypoint targetNode;

        [Tooltip("贝塞尔曲线的控制点偏移量。用于让移动路径产生弧度，而不是笔直的线。")]
        public Vector3 controlPointOffset;

        [Header("连接事件")]
        [Tooltip("在这条连线上触发的事件列表（例如：路过某处触发一段对话或扣除金币）")]
        public List<ConnectionEvent> events = new List<ConnectionEvent>();
    }

    /// <summary>
    /// 连接线事件数据：描述在两个节点之间移动时，在特定进度触发的行为。
    /// </summary>
    [System.Serializable]
    public class ConnectionEvent
    {
        [Tooltip("触发点在路径上的比例 ($0$ = 起点, $1$ = 终点)。例如 0.5 代表走到一半时触发。")]
        [Range(0.01f, 0.99f)]
        public float progressPoint = 0.5f;

        [Header("架构设计：命令模式 (Command Pattern)")]
        [Tooltip("拖入一个具体的事件配置文件 (ScriptableObject)。" +
                 "这允许你在不改代码的情况下，通过资源文件配置不同的事件逻辑。")]
        public BoardEventSO eventAction;

        [Tooltip("执行事件时的上下文参考目标（可选）。例如：事件是“看向某处”，这里就拖入那个目标物体。")]
        public Transform contextTarget;

        /// <summary>
        /// 运行时标记。由位移控制器管理，确保在一个移动周期内事件不会被重复触发。
        /// </summary>
        [HideInInspector] public bool hasTriggered = false;
    }

    /// <summary>
    /// 地图路点组件：棋盘地图的核心单元。
    /// 存储节点 ID、地块数据、以及通往其他节点的连接信息。
    /// </summary>
    public class MapWaypoint : MonoBehaviour
    {
        [Header("ID 设置")]
        [Tooltip("每个节点的唯一编号。在配置大量地块时，ID 是数据回溯和存档的关键。")]
        public int nodeID = 0;

        [Header("地块数据")]
        [Tooltip("引用 TileBase 资源，定义该格子的类型（金币格、事件格等）及其视觉配置。")]
        public TileBase tileData;

        [Header("连接关系")]
        [Tooltip("从该节点出发的所有可能路径。")]
        public List<WaypointConnection> connections = new List<WaypointConnection>();

        private void Awake()
        {
            // [性能优化] 预处理逻辑：
            // 在游戏初始化时，将每条连接线上的事件按进度点 (progressPoint) 从小到大排序。
            // 这样移动控制器在运行时只需线性检查，而不需要每帧使用 LinQ 进行高开销的排序操作。
            foreach (var conn in connections)
            {
                if (conn.events != null && conn.events.Count > 1)
                {
                    conn.events.Sort((a, b) => a.progressPoint.CompareTo(b.progressPoint));
                }
            }
        }

        /// <summary>
        /// 获取所有有效的后续移动目标。
        /// 包含防“原地踏步”逻辑：除非是死胡同，否则优先排除来时的路。
        /// </summary>
        /// <param name="incomingFrom">实体当前所在（即进入本节点前）的节点</param>
        /// <returns>可前往的节点列表</returns>
        public List<MapWaypoint> GetValidNextNodes(MapWaypoint incomingFrom)
        {
            List<MapWaypoint> results = new List<MapWaypoint>();
            for (int i = 0; i < connections.Count; i++)
            {
                MapWaypoint target = connections[i].targetNode;
                if (target == null || target == incomingFrom) continue;

                // 只要不是回头路，就是有效的下一跳
                results.Add(target);
            }

            // 特殊情况处理：如果是死胡同（只有一条路且就是来路）
            if (results.Count == 0 && incomingFrom != null)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    MapWaypoint target = connections[i].targetNode;
                    if (target == incomingFrom)
                    {
                        // 允许原路返回
                        results.Add(target);
                        break;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 查找指向特定目标的连接线详情（用于获取控制点、事件等）。
        /// </summary>
        public WaypointConnection GetConnectionTo(MapWaypoint node)
        {
            if (node == null) return null;
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].targetNode == node) return connections[i];
            }
            return null;
        }

        /// <summary>
        /// 批量获取通往多个目标的连接线。
        /// </summary>
        /// <param name="nodes">目标节点列表</param>
        public List<WaypointConnection> GetConnectionsTo(List<MapWaypoint> nodes)
        {
            List<WaypointConnection> results = new List<WaypointConnection>();
            if (nodes == null || nodes.Count == 0) return results;

            // [性能优化] 使用 HashSet 进行 O(1) 级别的查找
            // 避免在嵌套循环中使用 List.Contains 导致 O(N*M) 的复杂度
            HashSet<MapWaypoint> lookup = new HashSet<MapWaypoint>(nodes);
            for (int i = 0; i < connections.Count; i++)
            {
                MapWaypoint target = connections[i].targetNode;
                if (target != null && lookup.Contains(target))
                    results.Add(connections[i]);
            }
            return results;
        }

        /// <summary>
        /// 编辑器辅助绘制：在 Scene 窗口中可视化地块。
        /// </summary>
        private void OnDrawGizmos()
        {
            // 1. 绘制地块主体
            if (tileData != null)
            {
                // 根据配置文件的颜色绘制实心球，直观区分地块类型
                Gizmos.color = tileData.gizmoColor;
                Gizmos.DrawSphere(transform.position, 0.4f);
            }
            else
            {
                // 未配置数据时显示灰色线框球
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position, 0.4f);
            }

#if UNITY_EDITOR
            // 2. 绘制调试文字
            // 在球体上方显示 NodeID，方便策划在不点击物体的情况下快速排查路径逻辑
            Handles.Label(transform.position + Vector3.up * 0.8f, $"ID: {nodeID}", new GUIStyle()
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState() { textColor = Color.white }
            });
#endif
        }
    }
}