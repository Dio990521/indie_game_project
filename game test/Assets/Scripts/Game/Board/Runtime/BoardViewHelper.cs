using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Data; // 引用 TileBase 等数据
using IndieGame.Gameplay.Board.Runtime; // 引用 WaypointConnection

namespace IndieGame.Gameplay.Board.View
{
    /// <summary>
    /// 负责处理棋盘上的视觉表现（如生成选择光标、高亮路径等），
    /// 将“怎么显示”和“怎么玩”分离。
    /// </summary>
    public class BoardViewHelper : MonoBehaviour
    {
        [Header("Selection UI Settings")]
        [Tooltip("选择路径时的光标预制体")]
        public GameObject cursorPrefab;
        public float cursorOffsetDistance = 0.2f;
        public float cursorScale = 0.5f;

        [Header("Colors")]
        public Color normalColor = new Color(1, 1, 1, 0.5f);
        public Color highlightColor = Color.green;

        private List<GameObject> _spawnedCursors = new List<GameObject>();

        /// <summary>
        /// 在所有可选路径上生成光标
        /// </summary>
        public void ShowCursors(List<WaypointConnection> connections, Vector3 startNodePos)
        {
            ClearCursors();

            foreach (var conn in connections)
            {
                if (conn.targetNode == null) continue;

                GameObject cursor;
                if (cursorPrefab != null)
                {
                    cursor = Instantiate(cursorPrefab);
                }
                else
                {
                    // Fallback: 如果没有预制体，生成一个简单的球
                    cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(cursor.GetComponent<Collider>());
                }

                // 计算贝塞尔曲线上的点作为光标位置
                Vector3 p0 = startNodePos;
                Vector3 p2 = conn.targetNode.transform.position;
                Vector3 p1 = p0 + conn.controlPointOffset;

                cursor.transform.position = MapWaypoint.GetBezierPoint(cursorOffsetDistance, p0, p1, p2);
                cursor.transform.localScale = Vector3.one * cursorScale;
                
                // 确保有渲染器以便染色
                if(cursor.GetComponent<Renderer>() == null)
                    cursor.AddComponent<MeshRenderer>();

                _spawnedCursors.Add(cursor);
            }
        }

        /// <summary>
        /// 高亮指定索引的光标
        /// </summary>
        public void HighlightCursor(int activeIndex)
        {
            for (int i = 0; i < _spawnedCursors.Count; i++)
            {
                if (_spawnedCursors[i] == null) continue;

                var r = _spawnedCursors[i].GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = (i == activeIndex) ? highlightColor : normalColor;
                }
            }
        }

        public void ClearCursors()
        {
            foreach (var c in _spawnedCursors)
            {
                if (c) Destroy(c);
            }
            _spawnedCursors.Clear();
        }
    }
}