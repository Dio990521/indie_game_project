using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Editor
{
    /// <summary>
    /// 编辑器工具窗口：将场景中所有 MapWaypoint 的 Y 坐标贴合到 Unity Terrain 表面，
    /// 并自动修正贝塞尔控制点偏移量，使高低起伏路径的弧线过渡更加自然。
    ///
    /// 使用方式：菜单栏 Board Tools > Waypoint Terrain Snapper
    /// </summary>
    public class WaypointTerrainSnapper : EditorWindow
    {
        // --- 参数字段 ---
        private float _heightOffset       = 0f;    // 贴地后额外抬高的偏移量（避免穿模）
        private float _smoothFactor       = 0.5f;  // 控制点弧线抬升系数（0=平直，1=明显弧度）
        private bool  _applyControlPoints = true;  // 是否同步修正贝塞尔控制点
        private bool  _useRaycast         = false; // true=物理射线（支持任意碰撞体），false=Terrain.SampleHeight（更快更准）

        [MenuItem("Board Tools/Waypoint Terrain Snapper")]
        private static void Open()
        {
            GetWindow<WaypointTerrainSnapper>("Waypoint 贴地工具").minSize = new Vector2(320f, 260f);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("地形贴合设置", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            _heightOffset = EditorGUILayout.FloatField(
                new GUIContent("Y 偏移量", "贴合到地形表面后再额外抬高多少。建议 0.05~0.2，防止 Waypoint 球体穿入地表。"),
                _heightOffset);

            _useRaycast = EditorGUILayout.Toggle(
                new GUIContent("使用物理射线", "开启：支持任意带碰撞体的地形/Mesh。关闭：直接采样 Terrain（更快，仅适用于 Unity Terrain）。"),
                _useRaycast);

            EditorGUILayout.Space(4f);
            _applyControlPoints = EditorGUILayout.Toggle(
                new GUIContent("同步修正控制点", "贴地后自动调整每条连接的贝塞尔控制点 Y，让爬坡路线有自然弧度。"),
                _applyControlPoints);

            if (_applyControlPoints)
            {
                _smoothFactor = EditorGUILayout.Slider(
                    new GUIContent("弧度系数", "控制爬坡曲线的弓起程度。0 = 近乎直线，1 = 明显弧度。"),
                    _smoothFactor, 0f, 2f);
            }

            EditorGUILayout.Space(10f);

            // --- 主要按钮 ---
            using (new EditorGUI.DisabledGroupScope(Application.isPlaying))
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.5f, 0.9f, 0.6f);
                if (GUILayout.Button("贴合到地形（Snap to Terrain）", GUILayout.Height(38f)))
                    SnapToTerrain();

                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button("仅修正控制点（Smooth Control Points Only）", GUILayout.Height(30f)))
                    SmoothOnlyAll();

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("重置所有 Waypoint Y 为 0（Reset Heights）", GUILayout.Height(30f)))
                    ResetAllHeights();

                GUI.backgroundColor = prev;
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("请在编辑器非运行状态下使用此工具。", MessageType.Warning);
            }
        }

        // ------------------------------------------------------------------
        // 核心操作：贴合地形
        // ------------------------------------------------------------------

        private void SnapToTerrain()
        {
            MapWaypoint[] waypoints = FindAllWaypoints();
            if (waypoints == null) return;

            if (!_useRaycast && Terrain.activeTerrain == null)
            {
                EditorUtility.DisplayDialog("找不到 Terrain",
                    "场景中没有 Terrain 对象。\n请先创建 GameObject > 3D Object > Terrain，\n或勾选 [使用物理射线] 改用碰撞体模式。",
                    "确定");
                return;
            }

            Undo.SetCurrentGroupName("Snap Waypoints to Terrain");
            int group = Undo.GetCurrentGroup();

            int snapped = 0;
            foreach (MapWaypoint wp in waypoints)
            {
                float terrainY = SampleTerrainHeight(wp.transform.position);
                if (float.IsNegativeInfinity(terrainY)) continue; // 射线未命中

                Undo.RecordObject(wp.transform, "Snap Waypoint Y");
                Vector3 pos = wp.transform.position;
                pos.y = terrainY + _heightOffset;
                wp.transform.position = pos;
                snapped++;
            }

            if (_applyControlPoints)
                SmoothControlPoints(waypoints);

            Undo.CollapseUndoOperations(group);
            MarkSceneDirty();

            Debug.Log($"[WaypointTerrainSnapper] 贴地完成：{snapped}/{waypoints.Length} 个节点已贴合地形。");
        }

        // ------------------------------------------------------------------
        // 仅修正控制点
        // ------------------------------------------------------------------

        private void SmoothOnlyAll()
        {
            MapWaypoint[] waypoints = FindAllWaypoints();
            if (waypoints == null) return;

            Undo.SetCurrentGroupName("Smooth Waypoint Control Points");
            int group = Undo.GetCurrentGroup();

            SmoothControlPoints(waypoints);

            Undo.CollapseUndoOperations(group);
            MarkSceneDirty();
            Debug.Log("[WaypointTerrainSnapper] 控制点修正完成。");
        }

        // ------------------------------------------------------------------
        // 重置所有高度
        // ------------------------------------------------------------------

        private void ResetAllHeights()
        {
            if (!EditorUtility.DisplayDialog("确认重置",
                "将所有 MapWaypoint 的 Y 坐标清零，并重置所有连接的控制点 Y。\n此操作可撤销（Ctrl+Z）。",
                "确认重置", "取消")) return;

            MapWaypoint[] waypoints = FindAllWaypoints();
            if (waypoints == null) return;

            Undo.SetCurrentGroupName("Reset Waypoint Heights");
            int group = Undo.GetCurrentGroup();

            foreach (MapWaypoint wp in waypoints)
            {
                Undo.RecordObject(wp.transform, "Reset Y");
                Vector3 pos = wp.transform.position;
                pos.y = 0f;
                wp.transform.position = pos;

                Undo.RecordObject(wp, "Reset Control Points");
                foreach (WaypointConnection conn in wp.connections)
                    conn.controlPointOffset = Vector3.zero;
                EditorUtility.SetDirty(wp);
            }

            Undo.CollapseUndoOperations(group);
            MarkSceneDirty();
            Debug.Log("[WaypointTerrainSnapper] 所有 Waypoint 高度已重置为 0。");
        }

        // ------------------------------------------------------------------
        // 内部工具方法
        // ------------------------------------------------------------------

        /// <summary>
        /// 修正所有 Waypoint 连接的贝塞尔控制点，使高低差路径产生自然坡度弧线。
        /// controlPointOffset 是相对于起点（from）世界坐标的偏移量。
        ///
        /// 关键原理：控制点必须同时设置 XZ 和 Y，才能让横向移动和纵向爬坡同步。
        /// 做法：将控制点定位到 from→to 连线的中点，再在 Y 方向额外抬升产生弧度。
        /// 中点偏移 = (to.position - from.position) * 0.5f
        /// </summary>
        private void SmoothControlPoints(MapWaypoint[] waypoints)
        {
            foreach (MapWaypoint from in waypoints)
            {
                bool dirty = false;
                foreach (WaypointConnection conn in from.connections)
                {
                    if (conn.targetNode == null) continue;

                    Undo.RecordObject(from, "Smooth Control Point");

                    // 从起点到终点的向量的一半，即连线中点相对于起点的偏移
                    Vector3 midOffset = (conn.targetNode.transform.position - from.transform.position) * 0.5f;
                    // 高度差的绝对值决定弧度抬升量（上坡/下坡都向上弓起）
                    float arcLift = Mathf.Abs(midOffset.y) * _smoothFactor;
                    conn.controlPointOffset = new Vector3(midOffset.x, midOffset.y + arcLift, midOffset.z);

                    dirty = true;
                }
                if (dirty) EditorUtility.SetDirty(from);
            }
        }

        /// <summary>
        /// 采样指定世界坐标下的地形 Y 高度。
        /// 使用 Terrain.SampleHeight（快速）或 Physics.Raycast（通用）。
        /// 返回 float.NegativeInfinity 表示未命中。
        /// </summary>
        private float SampleTerrainHeight(Vector3 worldPos)
        {
            if (!_useRaycast)
            {
                // Terrain.SampleHeight 返回的是相对 Terrain 原点的高度，需要加 Terrain 的世界 Y
                Terrain t = Terrain.activeTerrain;
                return t.SampleHeight(worldPos) + t.transform.position.y;
            }

            // 物理射线：从高空向下打，命中任何碰撞体
            Vector3 origin = new Vector3(worldPos.x, 500f, worldPos.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f))
                return hit.point.y;

            return float.NegativeInfinity;
        }

        /// <summary>
        /// 获取场景中全部 MapWaypoint，包含非激活对象。
        /// 若未找到则弹窗提示并返回 null。
        /// </summary>
        private static MapWaypoint[] FindAllWaypoints()
        {
            MapWaypoint[] waypoints = FindObjectsByType<MapWaypoint>(FindObjectsSortMode.None);
            if (waypoints.Length > 0) return waypoints;

            EditorUtility.DisplayDialog("找不到节点",
                "场景中没有任何 MapWaypoint 组件。\n请确认 Board 地图已正确放置在场景中。",
                "确定");
            return null;
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkAllScenesDirty();
        }
    }
}
