using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Editor.Board
{
    [CustomEditor(typeof(BoardVisualizer))]
    public class BoardVisualizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Connection Tools", EditorStyles.boldLabel);

            // 一键按 ID 顺序自动连接所有节点
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Auto Connect All By ID (Chain)", GUILayout.Height(30)))
            {
                AutoConnectAllByID();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.HelpBox(
                "按 nodeID 从小到大依次双向连接：0↔1↔2↔3…\n" +
                "已有连接不会重复添加，运行后可手动调整岔路。",
                MessageType.Info);

            EditorGUILayout.Space();

            // 一键清空所有节点的连接
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Clear ALL Connections", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("确认清空",
                    "这将删除场景中所有 MapWaypoint 的连接，无法撤销批量 Undo（只能整体撤销）。\n确定继续？",
                    "确定清空", "取消"))
                {
                    ClearAllConnections();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Curve Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Flatten All Connections (Horizontal)"))
            {
                FlattenAllConnections();
            }
        }

        /// <summary>
        /// 按 nodeID 升序排列所有节点，然后相邻 ID 之间建立双向连接。
        /// 已存在的连接会跳过，不会重复添加。
        /// </summary>
        private void AutoConnectAllByID()
        {
            MapWaypoint[] allNodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (allNodes == null || allNodes.Length == 0)
            {
                Debug.LogWarning("[BoardVisualizer] 场景中未找到任何 MapWaypoint。");
                return;
            }

            // 按 nodeID 升序排列
            List<MapWaypoint> sorted = new List<MapWaypoint>(allNodes);
            sorted.Sort((a, b) => a.nodeID.CompareTo(b.nodeID));

            int linked = 0;

            // 相邻节点两两双向连接
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                MapWaypoint a = sorted[i];
                MapWaypoint b = sorted[i + 1];

                if (TryAddConnection(a, b)) linked++;
                if (TryAddConnection(b, a)) linked++;
            }

            Debug.Log($"[BoardVisualizer] 自动连接完成，新增连接数：{linked}，总节点数：{sorted.Count}");
        }

        /// <summary>
        /// 尝试从 from 到 to 添加一条单向连接。
        /// 若已存在则跳过，返回 false；成功添加返回 true。
        /// </summary>
        private bool TryAddConnection(MapWaypoint from, MapWaypoint to)
        {
            // 已存在则跳过
            if (from.connections.Exists(c => c.targetNode == to))
                return false;

            Undo.RecordObject(from, "Auto Connect All By ID");

            Vector3 midPoint = (to.transform.position - from.transform.position) * 0.5f;
            from.connections.Add(new WaypointConnection
            {
                targetNode = to,
                controlPointOffset = midPoint
            });

            EditorUtility.SetDirty(from);
            return true;
        }

        /// <summary>
        /// 清空场景中所有 MapWaypoint 的连接列表。
        /// </summary>
        private void ClearAllConnections()
        {
            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            for (int i = 0; i < nodes.Length; i++)
            {
                MapWaypoint node = nodes[i];
                if (node == null) continue;

                Undo.RecordObject(node, "Clear All Connections");
                node.connections.Clear();
                EditorUtility.SetDirty(node);
            }

            Debug.Log($"[BoardVisualizer] 已清空 {nodes.Length} 个节点的所有连接。");
        }

        /// <summary>
        /// 将所有连接的控制点水平化（Y 轴归零），消除曲线高度偏移。
        /// </summary>
        private void FlattenAllConnections()
        {
            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            for (int i = 0; i < nodes.Length; i++)
            {
                MapWaypoint node = nodes[i];
                if (node == null || node.connections == null) continue;

                Undo.RecordObject(node, "Flatten Connections");

                for (int j = 0; j < node.connections.Count; j++)
                {
                    WaypointConnection conn = node.connections[j];
                    if (conn == null || conn.targetNode == null) continue;

                    Vector3 from = node.transform.position;
                    Vector3 to = conn.targetNode.transform.position;
                    Vector3 mid = (to - from) * 0.5f;
                    mid.y = 0f;
                    conn.controlPointOffset = mid;
                }

                EditorUtility.SetDirty(node);
            }
        }
    }
}
