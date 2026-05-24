using UnityEditor;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Editor
{
    /// <summary>
    /// BoardRefreshManager 的自定义 Inspector。
    /// 在运行时实时展示上次刷新的格子分布统计。
    /// </summary>
    [CustomEditor(typeof(BoardRefreshManager))]
    public class BoardRefreshManagerEditor : UnityEditor.Editor
    {
        // 让 Inspector 在 Play Mode 下持续刷新，保持统计数据实时
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            // 先绘制默认字段（_enableRandomRefresh、_config 等）
            DrawDefaultInspector();

            var manager = (BoardRefreshManager)target;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("格子生成统计", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("统计数据在运行时显示。", MessageType.Info);
                return;
            }

            var stats = manager.GetCurrentStats();

            if (stats.Count == 0)
            {
                EditorGUILayout.HelpBox("尚未执行刷新，暂无统计数据。", MessageType.None);
                return;
            }

            // 计算总节点数
            int total = 0;
            foreach (var s in stats) total += s.count;
            EditorGUILayout.LabelField($"共刷新节点：{total} 个", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            // 绘制每种格子的统计行
            foreach (var s in stats)
            {
                Color tileColor = s.tile != null ? s.tile.gizmoColor : Color.gray;

                EditorGUILayout.BeginHorizontal();

                // 色块
                var colorRect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                colorRect.y += 2f;
                EditorGUI.DrawRect(colorRect, tileColor);

                // 名称
                EditorGUILayout.LabelField(s.label, GUILayout.MinWidth(80f));

                // 数量
                EditorGUILayout.LabelField($"{s.count} 个", GUILayout.Width(48f));

                // 百分比
                EditorGUILayout.LabelField($"{s.percentage * 100f:F1}%", GUILayout.Width(48f));

                EditorGUILayout.EndHorizontal();

                // 进度条
                var barBg = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(6f));
                EditorGUI.DrawRect(barBg, new Color(0.2f, 0.2f, 0.2f));
                var barFill = new Rect(barBg.x, barBg.y, barBg.width * s.percentage, barBg.height);
                EditorGUI.DrawRect(barFill, tileColor);

                EditorGUILayout.Space(2f);
            }

            // 堆叠总览色条
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("分布总览", EditorStyles.miniLabel);
            DrawStackedBar(stats, 14f);
        }

        private static void DrawStackedBar(System.Collections.Generic.List<BoardRefreshManager.TileStat> stats, float height)
        {
            Rect bar = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(bar, new Color(0.15f, 0.15f, 0.15f));

            float x = bar.x;
            foreach (var s in stats)
            {
                if (s.percentage <= 0f) continue;
                float segW = bar.width * s.percentage;
                Color c = s.tile != null ? s.tile.gizmoColor : Color.gray;
                EditorGUI.DrawRect(new Rect(x, bar.y, segW, bar.height), c);
                x += segW;
            }
        }
    }
}
