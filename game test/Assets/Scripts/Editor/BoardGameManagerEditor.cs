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
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("🎲 Roll Dice (Test)", GUILayout.Height(40)))
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
            GUI.backgroundColor = Color.white;
        }
    }
}