using UnityEngine;
using UnityEditor;
using IndieGame.Gameplay.Board.Runtime;
using System.Collections.Generic;
using IndieGame.Core.Utilities;

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

            // 只绘制曲线和控制点，去掉箭头
            for (int i = 0; i < waypoint.connections.Count; i++)
            {
                WaypointConnection conn = waypoint.connections[i];
                if (conn.targetNode == null) continue;
                if (IsBidirectionalConnection(waypoint, conn.targetNode) && waypoint.nodeID > conn.targetNode.nodeID)
                {
                    continue;
                }

                Vector3 startPos = waypoint.transform.position;
                Vector3 endPos = conn.targetNode.transform.position;
                Vector3 controlPointPos = startPos + conn.controlPointOffset;

                // 1. 绘制控制点手柄
                EditorGUI.BeginChangeCheck();
                Vector3 newControlPos = Handles.PositionHandle(controlPointPos, Quaternion.identity);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(waypoint, "Move Control Point");
                    conn.controlPointOffset = newControlPos - startPos;
                    // 实时刷新 LineRenderer
                    waypoint.GenerateVisualLines();
                }

                // 2. 绘制青色连接线 (仅在选中时显示高亮粗线，平时有 LineRenderer)
                Handles.color = Color.cyan;
                Vector3[] points = new Vector3[30];
                for (int j = 0; j < 30; j++)
                {
                    points[j] = BezierUtils.GetQuadraticBezierPoint(j / 29f, startPos, controlPointPos, endPos);
                }
                Handles.DrawAAPolyLine(3f, points);

                // 虚线辅助线
                Handles.color = new Color(1, 1, 1, 0.2f);
                Handles.DrawDottedLine(startPos, controlPointPos, 2f);
                Handles.DrawDottedLine(controlPointPos, endPos, 2f);
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
                current.GenerateVisualLines();
            }
            GUILayout.EndHorizontal();

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
            
            // ✅ 修改点：将偏移量改为两点之间的中点 (不加 Y 轴偏移)
            // 这样默认就是一条直线。用户如果想弯曲，再去手动拖动 Handle。
            Vector3 midPoint = (to.transform.position - from.transform.position) * 0.5f;
            
            from.connections.Add(new WaypointConnection
            {
                targetNode = to,
                controlPointOffset = midPoint // 此时控制点就在连线正中间，即直线
            });
            
            from.GenerateVisualLines(); 
        }

        private void ConnectNodesBidirectional(MapWaypoint a, MapWaypoint b)
        {
            ConnectNodes(a, b);
            ConnectNodes(b, a);
        }

        private bool IsBidirectionalConnection(MapWaypoint from, MapWaypoint to)
        {
            if (from == null || to == null) return false;
            return to.connections.Exists(c => c.targetNode == from);
        }
    }
}
