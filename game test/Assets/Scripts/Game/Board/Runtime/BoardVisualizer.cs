using System.Collections.Generic;
using IndieGame.Core.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘视觉化组件：负责在场景和游戏中绘制节点之间的连接线。
    /// 支持 Gizmos 预览和基于 LineRenderer 的运行时线条生成。
    /// </summary>
    public class BoardVisualizer : MonoBehaviour
    {
        [Header("视觉设置")]
        [SerializeField] private Color bidirectionalColor = new Color(1f, 1f, 1f, 0.6f); // 双向路径颜色（默认半透明白）
        [SerializeField] private Color oneWayColor = new Color(0.3f, 0.9f, 1f, 0.8f);      // 单向路径颜色（默认亮蓝色）
        [SerializeField] private float arrowSize = 0.5f;                                  // 单向箭头的大小
        [SerializeField] private float arrowAngle = 25f;                                 // 箭头的张开角度
        [SerializeField] private float lineYOffset = 0.1f;                                // 线条纵向偏移量（防止与地面重叠导致闪烁）

        [Header("运行时线条配置")]
        [SerializeField] private bool renderRuntimeLines = true;                          // 是否在播放模式下生成 LineRenderer 线条
        [SerializeField] private float lineWidth = 0.3f;                                  // 运行时的线条宽度
        [SerializeField] private int lineSegments = 20;                                   // 贝塞尔曲线的细分程度（值越大越平滑）
        [SerializeField] private Material lineMaterial;                                   // 线条使用的材质
        [SerializeField] private bool syncBidirectionalOffsets = true;                   // 是否自动同步双向路径的控制点（确保往返曲线重合）

        // 生成的运行时线条对象命名前缀，便于识别和清理
        private const string LinePrefix = "BoardLine_";

        private void Start()
        {
            // 如果未开启运行时渲染，则不执行初始化
            if (!renderRuntimeLines) return;
            RebuildRuntimeLines();
        }

        private void OnEnable()
        {
            // 监听场景切换事件，确保在加载新场景后重新绘制线条
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void OnDisable()
        {
            // 注销事件订阅，防止内存泄漏
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        /// <summary>
        /// 每帧由 Unity 编辑器调用，用于在 Scene 窗口绘制辅助线条。
        /// </summary>
        private void OnDrawGizmos()
        {
            // 如果正在运行且开启了运行时线框，则跳过 Gizmos 绘制，避免画面重叠
            if (Application.isPlaying && renderRuntimeLines) return;

            // 获取地图中所有的节点
            List<MapWaypoint> nodes = GetAllNodes();
            if (nodes == null || nodes.Count == 0) return;

            // 用于记录已处理的连接对（A->B 和 B->A 视为一对），防止重复绘制
            HashSet<string> processed = new HashSet<string>();

            for (int i = 0; i < nodes.Count; i++)
            {
                MapWaypoint from = nodes[i];
                if (from == null || from.connections == null) continue;

                for (int j = 0; j < from.connections.Count; j++)
                {
                    WaypointConnection conn = from.connections[j];
                    if (conn == null || conn.targetNode == null) continue;

                    MapWaypoint to = conn.targetNode;

                    // 生成唯一标识符，确保同一对节点间的线条只处理一次
                    string key = BuildPairKey(from, to);
                    if (!processed.Add(key)) continue;

                    // 检查是否存在反向连接
                    bool isBidirectional = HasConnection(to, from);
                    // 绘制简单的 Gizmos 线条
                    DrawConnection(from, to, isBidirectional);
                }
            }
        }

        /// <summary>
        /// 核心方法：彻底清空并重新构建运行时线条。
        /// 该方法会根据节点间的 Connection 信息生成带有 LineRenderer 的 GameObject。
        /// </summary>
        public void RebuildRuntimeLines()
        {
            // 1. 清理当前存在的线条对象
            ClearRuntimeLines();

            List<MapWaypoint> nodes = GetAllNodes();
            if (nodes == null || nodes.Count == 0) return;

            HashSet<string> processed = new HashSet<string>();
            int lineIndex = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                MapWaypoint from = nodes[i];
                if (from == null || from.connections == null) continue;

                for (int j = 0; j < from.connections.Count; j++)
                {
                    WaypointConnection conn = from.connections[j];
                    if (conn == null || conn.targetNode == null) continue;

                    MapWaypoint to = conn.targetNode;
                    string key = BuildPairKey(from, to);
                    if (!processed.Add(key)) continue;

                    bool isBidirectional = HasConnection(to, from);

                    // 2. 如果开启了同步功能，将反向连接的控制点强制对齐到正向连接的控制点位置
                    if (isBidirectional && syncBidirectionalOffsets)
                    {
                        SyncBidirectionalControlPoint(from, to, conn);
                    }

                    // 3. 实例化 LineRenderer 并绘制贝塞尔曲线
                    CreateRuntimeLine(lineIndex++, from, to, conn, isBidirectional);
                }
            }
        }

        /// <summary>
        /// 创建并配置单个运行时线条对象。
        /// </summary>
        private void CreateRuntimeLine(int index, MapWaypoint from, MapWaypoint to, WaypointConnection conn, bool isBidirectional)
        {
            GameObject lineObj = new GameObject($"{LinePrefix}{index}");
            // 将线条挂在起点节点下，这样当节点被销毁时，线条也会随之销毁
            lineObj.transform.SetParent(from.transform, false);
            lineObj.transform.localPosition = Vector3.zero;

            // 配置 LineRenderer 组件
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; // 使用世界坐标系进行计算
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
            lr.positionCount = lineSegments + 1;

            // 设置颜色反馈
            Color color = isBidirectional ? bidirectionalColor : oneWayColor;
            lr.startColor = color;
            lr.endColor = color;

            // --- 贝塞尔曲线计算 ---
            // 起点 (P0)、终点 (P2) 以及控制点 (P1)
            Vector3 p0 = from.transform.position + Vector3.up * lineYOffset;
            Vector3 p2 = to.transform.position + Vector3.up * lineYOffset;
            Vector3 p1 = from.transform.position + conn.controlPointOffset + Vector3.up * lineYOffset;

            for (int i = 0; i <= lineSegments; i++)
            {
                float t = i / (float)lineSegments;
                // 计算二次贝塞尔曲线上对应的点
                Vector3 pixel = BezierUtils.GetQuadraticBezierPoint(t, p0, p1, p2);
                lr.SetPosition(i, pixel);
            }
        }

        /// <summary>
        /// 扫描所有节点，销毁所有符合命名前缀的线条对象。
        /// </summary>
        private void ClearRuntimeLines()
        {
            List<MapWaypoint> nodes = GetAllNodes();
            if (nodes == null || nodes.Count == 0) return;

            for (int n = 0; n < nodes.Count; n++)
            {
                MapWaypoint node = nodes[n];
                if (node == null) continue;

                Transform parent = node.transform;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform child = parent.GetChild(i);
                    // 仅清理我们自己生成的线条对象，不要误删节点下的其他子物体
                    if (!child.name.StartsWith(LinePrefix)) continue;

                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        // 在编辑器环境下清理需要用 DestroyImmediate
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// 响应场景切换：清理并重建线条。
        /// </summary>
        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (!Application.isPlaying) return;

            ClearRuntimeLines();
            if (!renderRuntimeLines) return;

            RebuildRuntimeLines();
        }

        /// <summary>
        /// 强制同步双向连接的控制点偏移。
        /// 如果 A->B 设置了曲线偏移，此方法会自动计算 B->A 对应的偏移，确保往返路径完全重合。
        /// </summary>
        private void SyncBidirectionalControlPoint(MapWaypoint from, MapWaypoint to, WaypointConnection forwardConn)
        {
            if (from == null || to == null || forwardConn == null) return;
            WaypointConnection reverseConn = to.GetConnectionTo(from);
            if (reverseConn == null) return;

            // 计算正向控制点在世界空间的位置
            Vector3 controlWorld = from.transform.position + forwardConn.controlPointOffset;
            // 将该位置转换为反向连接（以 to 为起点）的相对偏移量
            reverseConn.controlPointOffset = controlWorld - to.transform.position;
        }

        /// <summary>
        /// 绘制 Gizmos 连接线及箭头。
        /// </summary>
        private void DrawConnection(MapWaypoint from, MapWaypoint to, bool isBidirectional)
        {
            Vector3 start = from.transform.position + Vector3.up * lineYOffset;
            Vector3 end = to.transform.position + Vector3.up * lineYOffset;

            Gizmos.color = isBidirectional ? bidirectionalColor : oneWayColor;
            // 注意：Gizmos 仅支持绘制直线，无法直接绘制贝塞尔曲线
            Gizmos.DrawLine(start, end);

#if UNITY_EDITOR
            // 如果是单向路径，绘制一个指示方向的箭头
            if (!isBidirectional)
            {
                DrawArrow(start, end);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 绘制方向箭头的具体逻辑（仅编辑器有效）。
        /// </summary>
        private void DrawArrow(Vector3 start, Vector3 end)
        {
            Vector3 dir = (end - start).normalized;
            if (dir == Vector3.zero) return;

            // 计算箭头的底部基准点
            Vector3 basePos = end - dir * arrowSize;
            // 计算箭头两侧的分支旋转
            Quaternion leftRot = Quaternion.AngleAxis(arrowAngle, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(-arrowAngle, Vector3.up);
            Vector3 left = leftRot * -dir;
            Vector3 right = rightRot * -dir;

            Handles.color = oneWayColor;
            // 绘制两根线段组成箭头形状
            Handles.DrawLine(end, basePos + left * (arrowSize * 0.5f));
            Handles.DrawLine(end, basePos + right * (arrowSize * 0.5f));
        }
#endif

        /// <summary>
        /// 生成一对节点的唯一 Key。
        /// 算法：排序两者的 ID（或 InstanceID），确保不论顺序如何，返回的字符串都一致。
        /// </summary>
        private string BuildPairKey(MapWaypoint a, MapWaypoint b)
        {
            int idA = a.nodeID;
            int idB = b.nodeID;

            // 优先按策划配置的 nodeID 排序
            if (idA < idB) return $"{idA}_{idB}";
            if (idA > idB) return $"{idB}_{idA}";

            // 如果 ID 相同（报错情况），则按引擎实例 ID 排序做兜底
            int instA = a.GetInstanceID();
            int instB = b.GetInstanceID();
            if (instA < instB) return $"{idA}_{idB}_{instA}_{instB}";
            return $"{idA}_{idB}_{instB}_{instA}";
        }

        /// <summary>
        /// 检查 from 节点是否直接连接到 to 节点。
        /// </summary>
        private bool HasConnection(MapWaypoint from, MapWaypoint to)
        {
            if (from == null || to == null || from.connections == null) return false;
            for (int i = 0; i < from.connections.Count; i++)
            {
                if (from.connections[i].targetNode == to) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取当前所有地块节点。
        /// 统一委托给 BoardMapManager 处理，确保数据源一致且高效。
        /// </summary>
        private List<MapWaypoint> GetAllNodes()
        {
            if (BoardMapManager.Instance == null) return null;
            return BoardMapManager.Instance.GetAllNodes();
        }
    }
}