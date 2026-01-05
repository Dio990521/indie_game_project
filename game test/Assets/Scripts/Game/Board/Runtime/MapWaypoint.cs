using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Data;

namespace IndieGame.Gameplay.Board.Runtime
{
    public class MapWaypoint : MonoBehaviour
    {
        [Header("Data")]
        public TileBase tileData;

        [Header("Connection")]
        // 支持多个后续节点（比如岔路口），目前Demo默认取第一个
        public List<MapWaypoint> nextWaypoints = new List<MapWaypoint>();

        private void OnDrawGizmos()
        {
            // 在Scene窗口画出名字和颜色，方便调试
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

            // 画出连接线
            Gizmos.color = Color.cyan;
            foreach (var next in nextWaypoints)
            {
                if (next != null)
                {
                    Gizmos.DrawLine(transform.position, next.transform.position);
                    // 画个箭头方向
                    Vector3 direction = (next.transform.position - transform.position).normalized;
                    Gizmos.DrawRay(transform.position, direction * 2f);
                }
            }
        }
    }
}