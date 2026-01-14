using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities; // 引用新的数学库
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

    [RequireComponent(typeof(LineRenderer))]
    public class MapWaypoint : MonoBehaviour
    {
        [Header("ID Settings")]
        [Tooltip("每个节点的唯一编号，用于自动连接")]
        public int nodeID = 0;

        [Header("Data")]
        public TileBase tileData;

        [Header("Connections")]
        public List<WaypointConnection> connections = new List<WaypointConnection>();

        [Header("Visualization")]
        public float lineWidth = 0.3f; // 线稍微细一点
        public int lineSegments = 20;

        private void Start()
        {
            GenerateVisualLines();
        }

        public void GenerateVisualLines()
        {
            // 清理旧的 LineRenderer 子物体（如果有）
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Line_")) Destroy(child.gameObject);
            }

            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].targetNode == null) continue;

                LineRenderer lr;
                if (i == 0)
                {
                    lr = GetComponent<LineRenderer>();
                }
                else
                {
                    GameObject childLine = new GameObject($"Line_{i}");
                    childLine.transform.SetParent(transform);
                    childLine.transform.localPosition = Vector3.zero;
                    lr = childLine.AddComponent<LineRenderer>();
                }

                SetupLineRenderer(lr, connections[i]);
            }
        }

        private void SetupLineRenderer(LineRenderer lr, WaypointConnection conn)
        {
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1, 1, 1, 0.5f); // 半透明白线，更优雅
            lr.endColor = new Color(1, 1, 1, 0.5f);
            lr.positionCount = lineSegments + 1;

            Vector3 p0 = transform.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 p1 = transform.position + conn.controlPointOffset;

            for (int j = 0; j <= lineSegments; j++)
            {
                float t = j / (float)lineSegments;
                Vector3 pixel = BezierUtils.GetQuadraticBezierPoint(t, p0, p1, p2);
                lr.SetPosition(j, pixel + Vector3.up * 0.1f);
            }
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