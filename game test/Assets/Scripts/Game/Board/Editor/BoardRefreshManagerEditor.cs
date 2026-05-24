using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Gameplay.Board.Editor
{
    /// <summary>
    /// BoardRefreshManager 的自定义 Inspector。
    /// 实时展示上次刷新的格子分布统计。
    /// </summary>
    [CustomEditor(typeof(BoardRefreshManager))]
    public class BoardRefreshManagerEditor : UnityEditor.Editor
    {
        private BoardRefreshManager _manager;

        private void OnEnable()
        {
            _manager = target as BoardRefreshManager;
            if (_manager != null)
                _manager.OnRefreshCompleted += OnRefreshCompleted;

            // 用 EditorApplication.update 在 play mode 下每帧强制重绘，
            // 比 RequiresConstantRepaint 更可靠
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;

            if (_manager != null)
                _manager.OnRefreshCompleted -= OnRefreshCompleted;
            _manager = null;
        }

        private void OnEditorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnRefreshCompleted() => Repaint();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manager = target as BoardRefreshManager;
            if (manager == null) return;

            // ── 手动刷新按钮 ────────────────────────────────────────────────
            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
            {
                if (GUILayout.Button("手动触发刷新（运行时）", GUILayout.Height(26f)))
                    manager.ManualRefresh();
            }

            // ── 统计面板 ────────────────────────────────────────────────────
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("格子生成统计", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("统计数据在运行时显示。", MessageType.Info);
                return;
            }

            var stats = manager.GetCurrentStats();

            if (stats == null || stats.Count == 0)
            {
                EditorGUILayout.HelpBox("尚未执行刷新，暂无统计数据。\n请确认：\n1. Config 已赋值\n2. 主开关已开启\n3. 场景中有未固定节点", MessageType.None);
                return;
            }

            int total = 0;
            foreach (var s in stats) total += s.count;
            EditorGUILayout.LabelField($"共刷新节点：{total} 个", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            foreach (var s in stats)
            {
                Color tileColor = s.tile != null ? s.tile.gizmoColor : Color.gray;

                EditorGUILayout.BeginHorizontal();

                var colorRect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                colorRect.y += 2f;
                EditorGUI.DrawRect(colorRect, tileColor);

                EditorGUILayout.LabelField(s.label, GUILayout.MinWidth(80f));
                EditorGUILayout.LabelField($"{s.count} 个", GUILayout.Width(48f));
                EditorGUILayout.LabelField($"{s.percentage * 100f:F1}%", GUILayout.Width(48f));

                EditorGUILayout.EndHorizontal();

                var barBg = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.ExpandWidth(true), GUILayout.Height(6f));
                EditorGUI.DrawRect(barBg, new Color(0.2f, 0.2f, 0.2f));
                EditorGUI.DrawRect(new Rect(barBg.x, barBg.y, barBg.width * s.percentage, barBg.height), tileColor);

                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("分布总览", EditorStyles.miniLabel);
            DrawStackedBar(stats, 14f);
        }

        private static void DrawStackedBar(List<BoardRefreshManager.TileStat> stats, float height)
        {
            Rect bar = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(bar, new Color(0.15f, 0.15f, 0.15f));

            float x = bar.x;
            foreach (var s in stats)
            {
                if (s.percentage <= 0f) continue;
                Color c = s.tile != null ? s.tile.gizmoColor : Color.gray;
                EditorGUI.DrawRect(new Rect(x, bar.y, bar.width * s.percentage, bar.height), c);
                x += bar.width * s.percentage;
            }
        }
    }
}
