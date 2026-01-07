using UnityEngine;
using UnityEditor;
using IndieGame.Gameplay.Board.Runtime;
using System.Collections.Generic;

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
                    points[j] = MapWaypoint.GetBezierPoint(j / 29f, startPos, controlPointPos, endPos);
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
            
            // 工具 1: 自动连接 ID + 1
            if (GUILayout.Button("Auto Link Next ID (ID+1)"))
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
            // 查找场景中所有 ID = current.ID + 1 的节点
            MapWaypoint[] allPoints = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            MapWaypoint target = null;

            foreach (var p in allPoints)
            {
                if (p.nodeID == current.nodeID + 1)
                {
                    target = p;
                    break;
                }
            }

            if (target != null)
            {
                ConnectNodes(current, target);
                Debug.Log($"<color=green>Connected: [{current.nodeID}] -> [{target.nodeID}]</color>");
            }
            else
            {
                Debug.LogWarning($"Could not find Node with ID {current.nodeID + 1}");
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
            // 检查重复连接
            if (from.connections.Exists(c => c.targetNode == to)) return;

            Undo.RecordObject(from, "Link Node");
            
            // 设置一个漂亮的默认曲线高度
            Vector3 midOffset = (to.transform.position - from.transform.position) / 2;
            midOffset.y = 0; // 水平中点
            Vector3 controlOffset = midOffset + Vector3.up * 2f; // 抬高2米

            from.connections.Add(new WaypointConnection
            {
                targetNode = to,
                controlPointOffset = controlOffset
            });
            
            from.GenerateVisualLines(); // 立即刷新显示
        }
    }
}