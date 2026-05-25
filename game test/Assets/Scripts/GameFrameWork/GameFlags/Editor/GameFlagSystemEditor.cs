using UnityEngine;
using UnityEditor;

namespace IndieGame.Core
{
    /// <summary>
    /// GameFlagSystem 自定义 Inspector：
    /// 运行时显示所有活跃 Flag，并支持直接点击切换 true/false，方便调试。
    /// 非运行时显示提示文字。
    /// </summary>
    [CustomEditor(typeof(GameFlagSystem))]
    public class GameFlagSystemEditor : UnityEditor.Editor
    {
        private string _newKey   = "";
        private bool   _newValue = true;

        public override void OnInspectorGUI()
        {
            // 标准字段（SaveID 等继承字段）不显示，保持 Inspector 干净
            // 如需查看基类字段可取消注释：
            // base.OnInspectorGUI();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入运行模式后可在此查看并修改所有 Flag。", MessageType.Info);
                return;
            }

            var system = (GameFlagSystem)target;

            // ── 当前所有 Flag ─────────────────────────────────────────────────

            EditorGUILayout.LabelField("当前 Flags", EditorStyles.boldLabel);

            var flags = system.GetAllFlags();

            if (flags.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无任何 Flag。", MessageType.None);
            }
            else
            {
                // 固定高度滚动区，Flag 较多时不撑爆 Inspector
                foreach (var kv in flags)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Key 标签（不可编辑）
                    EditorGUILayout.LabelField(kv.Key, GUILayout.ExpandWidth(true));

                    // 值开关：点击直接调用 SetFlag，触发事件和动画
                    bool newVal = EditorGUILayout.Toggle(kv.Value, GUILayout.Width(20));
                    if (newVal != kv.Value)
                        system.SetFlag(kv.Key, newVal);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("添加 / 设置 Flag", EditorStyles.boldLabel);

            // ── 添加新 Flag ───────────────────────────────────────────────────

            EditorGUILayout.BeginHorizontal();
            _newKey   = EditorGUILayout.TextField(_newKey, GUILayout.ExpandWidth(true));
            _newValue = EditorGUILayout.Toggle(_newValue, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_newKey));
            if (GUILayout.Button("Set Flag"))
            {
                system.SetFlag(_newKey.Trim(), _newValue);
                _newKey = "";
            }
            EditorGUI.EndDisabledGroup();

            // 每帧刷新，确保 Toggle 值实时反映运行时变化
            Repaint();
        }
    }
}
