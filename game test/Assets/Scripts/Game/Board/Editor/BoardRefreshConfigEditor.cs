using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IndieGame.Gameplay.Board.Data;

namespace IndieGame.Gameplay.Board.Editor
{
    /// <summary>
    /// BoardRefreshConfigSO 的自定义 Inspector，提供可视化概率分布展示。
    ///
    /// 功能：
    /// 普通格子 TilePool：每条目显示格子颜色标签 + 名称 + 权重 + 百分比，
    /// 底部绘制堆叠色条（Profiler 样式），一眼看清各格子占比。
    /// </summary>
    [CustomEditor(typeof(BoardRefreshConfigSO))]
    public class BoardRefreshConfigEditor : UnityEditor.Editor
    {
        private bool _showPool = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── 主开关 ──────────────────────────────────────────────────────
            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableRandomRefresh"),
                new GUIContent("开启随机刷新"));

            // ── 普通格子 TilePool 可视化 ─────────────────────────────────────
            EditorGUILayout.Space(8f);
            _showPool = EditorGUILayout.Foldout(_showPool, "格子概率分布", true, EditorStyles.foldoutHeader);
            if (_showPool)
            {
                EditorGUI.indentLevel++;
                var poolProp = serializedObject.FindProperty("tilePool");

                // 预先收集颜色和总权重，用于绘制
                float totalWeight = 0f;
                var colors = new List<Color>(poolProp.arraySize);

                for (int i = 0; i < poolProp.arraySize; i++)
                {
                    var entry = poolProp.GetArrayElementAtIndex(i);
                    float w = Mathf.Max(0f, entry.FindPropertyRelative("weight").floatValue);
                    totalWeight += w;
                    var tileAsset = entry.FindPropertyRelative("tile").objectReferenceValue as TileBase;
                    colors.Add(tileAsset != null ? tileAsset.gizmoColor : Color.gray);
                }

                // 绘制每条格子条目
                for (int i = 0; i < poolProp.arraySize; i++)
                {
                    var entry = poolProp.GetArrayElementAtIndex(i);
                    var tileProp = entry.FindPropertyRelative("tile");
                    var weightProp = entry.FindPropertyRelative("weight");
                    float w = Mathf.Max(0f, weightProp.floatValue);
                    float pct = totalWeight > 0f ? w / totalWeight : 0f;

                    EditorGUILayout.BeginHorizontal();

                    // 格子类型颜色色块
                    var colorRect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                    colorRect.y += 2f;
                    EditorGUI.DrawRect(colorRect, colors[i]);

                    // Tile 对象字段
                    EditorGUILayout.PropertyField(tileProp, GUIContent.none, GUILayout.MinWidth(100f));

                    // 权重字段
                    weightProp.floatValue = Mathf.Max(0f,
                        EditorGUILayout.FloatField(weightProp.floatValue, GUILayout.Width(52f)));

                    // 百分比文本
                    EditorGUILayout.LabelField($"{pct * 100f:F1}%", GUILayout.Width(48f));

                    // 删除按钮
                    if (GUILayout.Button("－", GUILayout.Width(22f)))
                    {
                        poolProp.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                // 添加按钮
                EditorGUILayout.Space(2f);
                if (GUILayout.Button("＋ 添加格子", GUILayout.Height(22f)))
                    poolProp.InsertArrayElementAtIndex(poolProp.arraySize);

                // 堆叠色条
                if (totalWeight > 0f && poolProp.arraySize > 0)
                {
                    EditorGUILayout.Space(4f);
                    var weights = new List<float>(poolProp.arraySize);
                    for (int i = 0; i < poolProp.arraySize; i++)
                        weights.Add(Mathf.Max(0f,
                            poolProp.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue));
                    DrawStackedBar(weights, colors, 18f);
                    EditorGUILayout.Space(2f);
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------
        // 堆叠横条
        // ------------------------------------------------------------------

        private static void DrawStackedBar(List<float> weights, List<Color> colors, float height)
        {
            float total = 0f;
            foreach (var w in weights) total += w;
            if (total <= 0f) return;

            Rect bar = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(bar, new Color(0.15f, 0.15f, 0.15f));

            float x = bar.x;
            for (int i = 0; i < weights.Count && i < colors.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                float segW = bar.width * (weights[i] / total);
                EditorGUI.DrawRect(new Rect(x, bar.y, segW, bar.height), colors[i]);
                x += segW;
            }
        }
    }
}
