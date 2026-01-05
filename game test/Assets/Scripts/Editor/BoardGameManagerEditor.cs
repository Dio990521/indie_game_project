using UnityEngine;
using UnityEditor;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Editor.Board
{
    [CustomEditor(typeof(BoardGameManager))]
    public class BoardGameManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BoardGameManager manager = (BoardGameManager)target;

            GUILayout.Space(20);
            
            GUILayout.BeginHorizontal();
            
            // 绿色掷骰子按钮
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("🎲 Roll Dice", GUILayout.Height(40)))
            {
                if (Application.isPlaying)
                {
                    manager.RollDice();
                }
                else
                {
                    Debug.LogWarning("请先运行游戏 (Play Mode) 再测试掷骰子。");
                }
            }

            // 红色重置按钮
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // 浅红
            if (GUILayout.Button("🔄 Reset", GUILayout.Height(40)))
            {
                if (Application.isPlaying)
                {
                    manager.ResetToStart();
                }
            }
            
            GUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }
    }
}