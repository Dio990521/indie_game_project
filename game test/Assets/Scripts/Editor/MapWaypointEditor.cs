using UnityEngine;
using UnityEditor;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Editor.Board
{
    [CustomEditor(typeof(MapWaypoint))]
    [CanEditMultipleObjects]
    public class MapWaypointEditor : UnityEditor.Editor
    {
        // Scene 窗口交互逻辑
        private void OnSceneGUI()
        {
            MapWaypoint waypoint = (MapWaypoint)target;

            if (waypoint.connections == null) return;

            for (int i = 0; i < waypoint.connections.Count; i++)
            {
                WaypointConnection conn = waypoint.connections[i];
                if (conn.targetNode == null) continue;

                Vector3 startPos = waypoint.transform.position;
                Vector3 endPos = conn.targetNode.transform.position;
                // 计算控制点的世界坐标
                Vector3 controlPointPos = startPos + conn.controlPointOffset;

                // 1. 绘制控制点手柄 (FreeMoveHandle)
                EditorGUI.BeginChangeCheck();
                Vector3 newControlPos = Handles.PositionHandle(controlPointPos, Quaternion.identity);
                // 或者用更小的圆点手柄: 
                // Vector3 newControlPos = Handles.FreeMoveHandle(controlPointPos, Quaternion.identity, 0.5f, Vector3.zero, Handles.SphereHandleCap);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(waypoint, "Move Control Point");
                    // 转回局部坐标保存
                    conn.controlPointOffset = newControlPos - startPos;
                }

                // 2. 绘制粗连接线 (贝塞尔曲线)
                // 这里的切线计算是为了 DrawBezier 接口，实际上我们是二次贝塞尔，但 Handles.DrawBezier 是三次的。
                // 我们可以用 Handles.DrawAAPolyLine 来画更精确的二次贝塞尔，或者近似模拟。
                // 为了简单且好看，我们这里用 DrawBezier 近似，或者手动采样画线。
                // 既然我们在 Runtime 已经写了采样算法，Editor 里直接画采样线最准确。
                
                Handles.color = Color.cyan;
                Vector3[] points = new Vector3[20];
                for (int j = 0; j < 20; j++)
                {
                    points[j] = MapWaypoint.GetBezierPoint(j / 19f, startPos, controlPointPos, endPos);
                }
                Handles.DrawAAPolyLine(5f, points); // 5f 是线宽，很粗！

                // 3. 绘制连线中间的辅助虚线（指向控制点）
                Handles.color = new Color(1, 1, 1, 0.3f);
                Handles.DrawDottedLine(startPos, controlPointPos, 5f);
                Handles.DrawDottedLine(controlPointPos, endPos, 5f);

                // 4. 绘制箭头 (画在曲线中点)
                Vector3 midPoint = MapWaypoint.GetBezierPoint(0.5f, startPos, controlPointPos, endPos);
                Vector3 nextPoint = MapWaypoint.GetBezierPoint(0.51f, startPos, controlPointPos, endPos);
                Vector3 direction = (nextPoint - midPoint).normalized;
                
                Handles.color = Color.yellow;
                if (direction != Vector3.zero)
                {
                    Handles.ArrowHandleCap(0, midPoint, Quaternion.LookRotation(direction), 1.5f, EventType.Repaint);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MapWaypoint current = (MapWaypoint)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Link to Nearest Waypoint"))
            {
                LinkToNearest(current);
            }
        }

        private void LinkToNearest(MapWaypoint current)
        {
            MapWaypoint[] allPoints = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            MapWaypoint nearest = null;
            float minDst = float.MaxValue;

            foreach (var p in allPoints)
            {
                if (p == current) continue;
                // 检查是否已经连接
                if (current.connections.Exists(c => c.targetNode == p)) continue;

                float dst = Vector3.Distance(current.transform.position, p.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    nearest = p;
                }
            }

            if (nearest != null)
            {
                Undo.RecordObject(current, "Link Waypoint");
                // 默认控制点在两点中间稍微抬高
                Vector3 midPointOffset = (nearest.transform.position - current.transform.position) / 2 + Vector3.up * 2;
                
                current.connections.Add(new WaypointConnection 
                { 
                    targetNode = nearest,
                    controlPointOffset = midPointOffset
                });
                Debug.Log($"Linked {current.name} to {nearest.name}");
            }
            else
            {
                Debug.LogWarning("No suitable waypoint found to link.");
            }
        }
    }
}