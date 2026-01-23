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
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Flatten All Connections (Horizontal)"))
            {
                FlattenAllConnections();
            }
        }

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
            }
        }
    }
}
