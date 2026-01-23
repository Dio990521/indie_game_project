using System.Collections.Generic;
using IndieGame.Core.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IndieGame.Gameplay.Board.Runtime
{
    public class BoardVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Color bidirectionalColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color oneWayColor = new Color(0.3f, 0.9f, 1f, 0.8f);
        [SerializeField] private float arrowSize = 0.5f;
        [SerializeField] private float arrowAngle = 25f;
        [SerializeField] private float lineYOffset = 0.1f;
        [SerializeField] private bool renderRuntimeLines = true;
        [SerializeField] private float lineWidth = 0.3f;
        [SerializeField] private int lineSegments = 20;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private bool syncBidirectionalOffsets = true;

        private const string LinePrefix = "BoardLine_";

        private void Start()
        {
            if (!renderRuntimeLines) return;
            RebuildRuntimeLines();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying && renderRuntimeLines) return;
            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            HashSet<string> processed = new HashSet<string>();

            for (int i = 0; i < nodes.Length; i++)
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
                    DrawConnection(from, to, isBidirectional);
                }
            }
        }

        public void RebuildRuntimeLines()
        {
            ClearRuntimeLines();

            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            HashSet<string> processed = new HashSet<string>();
            int lineIndex = 0;

            for (int i = 0; i < nodes.Length; i++)
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
                    if (isBidirectional && syncBidirectionalOffsets)
                    {
                        SyncBidirectionalControlPoint(from, to, conn);
                    }
                    CreateRuntimeLine(lineIndex++, from, to, conn, isBidirectional);
                }
            }
        }

        private void CreateRuntimeLine(int index, MapWaypoint from, MapWaypoint to, WaypointConnection conn, bool isBidirectional)
        {
            GameObject lineObj = new GameObject($"{LinePrefix}{index}");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
            lr.positionCount = lineSegments + 1;

            Color color = isBidirectional ? bidirectionalColor : oneWayColor;
            lr.startColor = color;
            lr.endColor = color;

            Vector3 p0 = from.transform.position + Vector3.up * lineYOffset;
            Vector3 p2 = to.transform.position + Vector3.up * lineYOffset;
            Vector3 p1 = from.transform.position + conn.controlPointOffset + Vector3.up * lineYOffset;

            for (int i = 0; i <= lineSegments; i++)
            {
                float t = i / (float)lineSegments;
                Vector3 pixel = BezierUtils.GetQuadraticBezierPoint(t, p0, p1, p2);
                lr.SetPosition(i, pixel);
            }
        }

        private void ClearRuntimeLines()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith(LinePrefix)) continue;
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (!Application.isPlaying) return;

            ClearRuntimeLines();
            if (!renderRuntimeLines) return;

            MapWaypoint[] nodes = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (nodes == null || nodes.Length == 0) return;

            RebuildRuntimeLines();
        }

        private void SyncBidirectionalControlPoint(MapWaypoint from, MapWaypoint to, WaypointConnection forwardConn)
        {
            if (from == null || to == null || forwardConn == null) return;
            WaypointConnection reverseConn = to.GetConnectionTo(from);
            if (reverseConn == null) return;

            Vector3 controlWorld = from.transform.position + forwardConn.controlPointOffset;
            reverseConn.controlPointOffset = controlWorld - to.transform.position;
        }

        private void DrawConnection(MapWaypoint from, MapWaypoint to, bool isBidirectional)
        {
            Vector3 start = from.transform.position + Vector3.up * lineYOffset;
            Vector3 end = to.transform.position + Vector3.up * lineYOffset;

            Gizmos.color = isBidirectional ? bidirectionalColor : oneWayColor;
            Gizmos.DrawLine(start, end);

#if UNITY_EDITOR
            if (!isBidirectional)
            {
                DrawArrow(start, end);
            }
#endif
        }

#if UNITY_EDITOR
        private void DrawArrow(Vector3 start, Vector3 end)
        {
            Vector3 dir = (end - start).normalized;
            if (dir == Vector3.zero) return;

            Vector3 basePos = end - dir * arrowSize;
            Quaternion leftRot = Quaternion.AngleAxis(arrowAngle, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(-arrowAngle, Vector3.up);
            Vector3 left = leftRot * -dir;
            Vector3 right = rightRot * -dir;

            Handles.color = oneWayColor;
            Handles.DrawLine(end, basePos + left * (arrowSize * 0.5f));
            Handles.DrawLine(end, basePos + right * (arrowSize * 0.5f));
        }
#endif

        private string BuildPairKey(MapWaypoint a, MapWaypoint b)
        {
            int idA = a.nodeID;
            int idB = b.nodeID;
            if (idA < idB) return $"{idA}_{idB}";
            if (idA > idB) return $"{idB}_{idA}";

            int instA = a.GetInstanceID();
            int instB = b.GetInstanceID();
            if (instA < instB) return $"{idA}_{idB}_{instA}_{instB}";
            return $"{idA}_{idB}_{instB}_{instA}";
        }

        private bool HasConnection(MapWaypoint from, MapWaypoint to)
        {
            if (from == null || to == null || from.connections == null) return false;
            for (int i = 0; i < from.connections.Count; i++)
            {
                if (from.connections[i].targetNode == to) return true;
            }
            return false;
        }
    }
}
