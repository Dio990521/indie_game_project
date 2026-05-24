using UnityEngine;
using UnityEditor;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Editor.Board
{
    [CustomEditor(typeof(MapWaypoint))]
    [CanEditMultipleObjects]
    public class MapWaypointEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            MapWaypoint waypoint = (MapWaypoint)target;

            if (waypoint.connections == null) return;

            for (int i = 0; i < waypoint.connections.Count; i++)
            {
                WaypointConnection conn = waypoint.connections[i];
                if (conn.targetNode == null) continue;

                Vector3 startPos = waypoint.transform.position;
                Vector3 controlPointPos = startPos + conn.controlPointOffset;

                // 1. 绘制控制点手柄
                EditorGUI.BeginChangeCheck();
                Vector3 newControlPos = Handles.PositionHandle(controlPointPos, Quaternion.identity);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(waypoint, "Move Control Point");
                    conn.controlPointOffset = newControlPos - startPos;
                    // 连接线由 BoardVisualizer 绘制
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MapWaypoint current = (MapWaypoint)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("🔗 Connectivity Tools", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            
            // 工具 1: 自动连接 ID + 1 / ID - 1 (双向)
            if (GUILayout.Button("Auto Link Adjacent IDs (ID+1 / ID-1)"))
            {
                AutoLinkNextID(current);
            }

            // 工具 2: 断开所有连接
            if (GUILayout.Button("Clear Links"))
            {
                Undo.RecordObject(current, "Clear Links");
                current.connections.Clear();
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Reset Connections to Straight Lines"))
            {
                ResetConnectionsToStraightLines(current);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("提示: 若要连接岔路(Fork)，请选中两个节点(起点和终点)，然后在下方点击 'Link Selected'.", MessageType.Info);

            // 工具 3: 连接选中的两个物体 (处理岔路的神器)
            if (GUILayout.Button("Link Selected Objects (From -> To)"))
            {
                LinkSelectedNodes();
            }
        }

        private void AutoLinkNextID(MapWaypoint current)
        {
            // 查找场景中所有 ID = current.ID + 1 / -1 的节点
            MapWaypoint[] allPoints = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            MapWaypoint next = null;
            MapWaypoint prev = null;

            foreach (var p in allPoints)
            {
                if (p.nodeID == current.nodeID + 1)
                {
                    next = p;
                }
                else if (p.nodeID == current.nodeID - 1)
                {
                    prev = p;
                }
            }

            if (next != null)
            {
                ConnectNodesBidirectional(current, next);
                Debug.Log($"<color=green>Connected: [{current.nodeID}] <-> [{next.nodeID}]</color>");
            }
            if (prev != null)
            {
                ConnectNodesBidirectional(current, prev);
                Debug.Log($"<color=green>Connected: [{current.nodeID}] <-> [{prev.nodeID}]</color>");
            }

            if (next == null && prev == null)
            {
                Debug.LogWarning($"Could not find Node with ID {current.nodeID + 1} or {current.nodeID - 1}");
            }
        }

        private void LinkSelectedNodes()
        {
            // 获取编辑器中选中的所有物体
            GameObject[] selectedGOs = Selection.gameObjects;
            if (selectedGOs.Length != 2)
            {
                Debug.LogError("请准确选中 2 个 MapWaypoint 节点来建立连接！");
                return;
            }

            MapWaypoint fromNode = selectedGOs[0].GetComponent<MapWaypoint>();
            MapWaypoint toNode = selectedGOs[1].GetComponent<MapWaypoint>();

            // 简单的逻辑判断：ID小的连向ID大的，或者按选择顺序
            // 这里我们假设第一个选的是起点，第二个是终点。但Unity的选择顺序有时难判断。
            // 不如直接对比 ID，ID小的连向大的。
            if (fromNode.nodeID > toNode.nodeID)
            {
                var temp = fromNode;
                fromNode = toNode;
                toNode = temp;
            }

            if (fromNode != null && toNode != null)
            {
                ConnectNodes(fromNode, toNode);
                Debug.Log($"<color=green>Manual Linked: [{fromNode.nodeID}] -> [{toNode.nodeID}]</color>");
            }
        }

        private void ConnectNodes(MapWaypoint from, MapWaypoint to)
        {
            if (from.connections.Exists(c => c.targetNode == to)) return;

            Undo.RecordObject(from, "Link Node");

            // 默认控制点在连线中点，即直线。用户可手动拖动 Handle 来弯曲。
            Vector3 midPoint = (to.transform.position - from.transform.position) * 0.5f;

            from.connections.Add(new WaypointConnection
            {
                targetNode = to,
                controlPointOffset = midPoint
            });

            // 必须调用 SetDirty，否则 Unity 不会将 Scene 标记为已修改，
            // 进入 Play Mode 时的快照将不包含此次连接，退出后连接会丢失。
            EditorUtility.SetDirty(from);
        }

        private void ConnectNodesBidirectional(MapWaypoint a, MapWaypoint b)
        {
            ConnectNodes(a, b);
            ConnectNodes(b, a);
        }

        private void ResetConnectionsToStraightLines(MapWaypoint current)
        {
            if (current == null || current.connections == null) return;

            Undo.RecordObject(current, "Reset Connections to Straight Lines");
            Vector3 start = current.transform.position;

            for (int i = 0; i < current.connections.Count; i++)
            {
                WaypointConnection conn = current.connections[i];
                if (conn.targetNode == null) continue;

                Vector3 end = conn.targetNode.transform.position;
                Vector3 mid = (start + end) * 0.5f;
                conn.controlPointOffset = mid - start;
            }

            EditorUtility.SetDirty(current);
        }

    }
}
