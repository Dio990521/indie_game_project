using UnityEngine;
using UnityEditor;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Editor.Board
{
    [CustomEditor(typeof(MapWaypoint))]
    [CanEditMultipleObjects]
    public class MapWaypointEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MapWaypoint current = (MapWaypoint)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Link to Nearest Waypoint (Forward)"))
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
                // 防止自己连自己，或者已经是连接过的
                if (current.nextWaypoints.Contains(p)) continue;

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
                current.nextWaypoints.Add(nearest);
                Debug.Log($"Linked {current.name} to {nearest.name}");
            }
            else
            {
                Debug.LogWarning("No suitable waypoint found to link.");
            }
        }
    }
}