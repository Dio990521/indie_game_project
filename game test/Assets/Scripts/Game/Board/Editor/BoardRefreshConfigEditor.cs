using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IndieGame.Gameplay.Board.Data;

namespace IndieGame.Gameplay.Board.Editor
{
    /// <summary>
    /// BoardRefreshConfigSO 的自定义 Inspector，提供可视化概率分布展示。
    ///
    /// 每条目显示：颜色标签 + 名称 + 权重 + 百分比 + 最小数量 + 最大数量。
    /// 底部绘制堆叠色条，一眼看清各格子占比。
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

                // 预先收集颜色和总权重
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

                // 列标题
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(18f);
                EditorGUILayout.LabelField("格子",      GUILayout.MinWidth(80f));
                EditorGUILayout.LabelField("权重",      GUILayout.Width(52f));
                EditorGUILayout.LabelField("占比",      GUILayout.Width(48f));
                EditorGUILayout.LabelField("最少",      GUILayout.Width(40f));
                EditorGUILayout.LabelField("最多",      GUILayout.Width(40f));
                GUILayout.Space(26f);
                EditorGUILayout.EndHorizontal();

                // 绘制每条格子条目
                for (int i = 0; i < poolProp.arraySize; i++)
                {
                    var entry      = poolProp.GetArrayElementAtIndex(i);
                    var tileProp   = entry.FindPropertyRelative("tile");
                    var weightProp = entry.FindPropertyRelative("weight");
                    var minProp    = entry.FindPropertyRelative("minCount");
                    var maxProp    = entry.FindPropertyRelative("maxCount");

                    float w   = Mathf.Max(0f, weightProp.floatValue);
                    float pct = totalWeight > 0f ? w / totalWeight : 0f;

                    // 校验 min <= max（max=0 表示无限制，不校验）
                    bool hasConflict = maxProp.intValue > 0 && minProp.intValue > maxProp.intValue;

                    EditorGUILayout.BeginHorizontal();

                    // 格子类型颜色色块
                    var colorRect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                    colorRect.y += 2f;
                    EditorGUI.DrawRect(colorRect, colors[i]);

                    // Tile 对象字段
                    EditorGUILayout.PropertyField(tileProp, GUIContent.none, GUILayout.MinWidth(80f));

                    // 权重字段
                    weightProp.floatValue = Mathf.Max(0f,
                        EditorGUILayout.FloatField(weightProp.floatValue, GUILayout.Width(52f)));

                    // 百分比文本
                    EditorGUILayout.LabelField($"{pct * 100f:F1}%", GUILayout.Width(48f));

                    // 最小数量
                    Color prevColor = GUI.color;
                    if (hasConflict) GUI.color = new Color(1f, 0.5f, 0.5f);
                    minProp.intValue = Mathf.Max(0,
                        EditorGUILayout.IntField(minProp.intValue, GUILayout.Width(40f)));

                    // 最大数量（0 = 不限制）
                    maxProp.intValue = Mathf.Max(0,
                        EditorGUILayout.IntField(maxProp.intValue, GUILayout.Width(40f)));
                    GUI.color = prevColor;

                    // 删除按钮
                    if (GUILayout.Button("－", GUILayout.Width(22f)))
                    {
                        poolProp.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();

                    // min > max 冲突提示
                    if (hasConflict)
                    {
                        EditorGUILayout.HelpBox(
                            $"最小数量（{minProp.intValue}）不能大于最大数量（{maxProp.intValue}）",
                            MessageType.Warning);
                    }
                }

                // 添加按钮
                EditorGUILayout.Space(2f);
                if (GUILayout.Button("＋ 添加格子", GUILayout.Height(22f)))
                    poolProp.InsertArrayElementAtIndex(poolProp.arraySize);

                // 字段说明
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    "最少/最多 = 0 表示不限制；刷新时先保证最少数量，再按权重随机填充剩余。",
                    EditorStyles.wordWrappedMiniLabel);

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
