using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Data; // 引用 TileBase 等数据
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Core.Utilities; // 引用 WaypointConnection

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
        private SimpleGameObjectPool _cursorPool;
        private List<GameObject> _activeCursors = new List<GameObject>();

        private void Awake()
        {
            if (cursorPrefab != null)
            {
                // 初始化对象池，预生成 3 个备用
                // 创建一个子物体作为池子的容器，保持 Hierarchy 整洁
                GameObject poolRoot = new GameObject("CursorPool");
                poolRoot.transform.SetParent(this.transform);
                
                _cursorPool = new SimpleGameObjectPool(cursorPrefab, poolRoot.transform, 3);
            }
        }

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
                    cursor = _cursorPool.Get();
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

                cursor.transform.position = BezierUtils.GetQuadraticBezierPoint(cursorOffsetDistance, p0, p1, p2);
                cursor.transform.localScale = Vector3.one * cursorScale;
                
                // 确保有渲染器以便染色
                if(cursor.GetComponent<Renderer>() == null)
                    cursor.AddComponent<MeshRenderer>();

                _activeCursors.Add(cursor);
            }
        }

        /// <summary>
        /// 高亮指定索引的光标
        /// </summary>
        public void HighlightCursor(int activeIndex)
        {
            for (int i = 0; i < _activeCursors.Count; i++)
            {
                if (_activeCursors[i] == null) continue;

                var r = _activeCursors[i].GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = (i == activeIndex) ? highlightColor : normalColor;
                }
            }
        }

        public void ClearCursors()
        {
            foreach (var c in _activeCursors)
            {
                if (c) Destroy(c);
            }
            _activeCursors.Clear();
        }
    }
}