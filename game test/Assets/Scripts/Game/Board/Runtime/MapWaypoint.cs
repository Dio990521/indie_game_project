using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Data;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 定义从一个格子到另一个格子的连接数据
    /// </summary>
    [System.Serializable]
    public class WaypointConnection
    {
        public MapWaypoint targetNode;
        // 控制点偏移量（相对于起始节点的局部坐标），用于调节曲线弯曲度
        public Vector3 controlPointOffset = new Vector3(0, 5, 0); 
    }

    [RequireComponent(typeof(LineRenderer))]
    public class MapWaypoint : MonoBehaviour
    {
        [Header("Data")]
        public TileBase tileData;

        [Header("Connections")]
        // 这里改用了自定义类，支持曲线设置
        public List<WaypointConnection> connections = new List<WaypointConnection>();

        [Header("Visualization")]
        public float lineWidth = 0.5f;
        public int lineSegments = 20; // 曲线平滑度

        private void Start()
        {
            // 游戏运行时生成可见的连接线
            GenerateVisualLines();
        }

        private void GenerateVisualLines()
        {
            // 我们使用子物体来渲染多条线，因为一个LineRenderer只能画一条连续的线
            // 如果连接数 > 0，主物体上的 LineRenderer 画第一条
            // 额外的连接创建子物体画
            
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
            lr.material = new Material(Shader.Find("Sprites/Default")); // 暂时用默认材质
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.positionCount = lineSegments + 1;

            Vector3 p0 = transform.position;
            Vector3 p2 = conn.targetNode.transform.position;
            Vector3 p1 = transform.position + conn.controlPointOffset; // 计算世界坐标控制点

            for (int j = 0; j <= lineSegments; j++)
            {
                float t = j / (float)lineSegments;
                Vector3 pixel = GetBezierPoint(t, p0, p1, p2);
                lr.SetPosition(j, pixel + Vector3.up * 0.1f); // 稍微抬高一点防止穿模
            }
        }

        // 静态工具方法：二次贝塞尔曲线公式
        // B(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
        public static Vector3 GetBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            t = Mathf.Clamp01(t);
            float u = 1 - t;
            return u * u * p0 + 2 * u * t * p1 + t * t * p2;
        }

        private void OnDrawGizmos()
        {
            // 简单的球体显示，复杂的连线交给 Editor 脚本处理
            if (tileData != null)
            {
                Gizmos.color = tileData.gizmoColor;
                Gizmos.DrawSphere(transform.position + Vector3.up * 0.5f, 0.3f);
            }
            else
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.3f);
            }
        }
    }
}