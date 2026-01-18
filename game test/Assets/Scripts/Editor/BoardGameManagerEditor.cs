using UnityEngine;
using UnityEditor;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Core; // 必须引用：访问 GameManager 和 GameState

namespace IndieGame.Editor.Board
{
    [CustomEditor(typeof(BoardGameManager))]
    public class BoardGameManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 1. 绘制原本的 Inspector 属性 (Player Token, Nodes 等)
            DrawDefaultInspector();

            BoardGameManager manager = (BoardGameManager)target;

            GUILayout.Space(20);
            
            // ==================== 🎮 游戏状态控制区 ====================
            EditorGUILayout.LabelField("🎮 Game State Control", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                // 获取 GameManager 实例 (防空检查)
                var gameManager = GameManager.Instance;
                if (gameManager != null)
                {
                    GameState currentState = gameManager.CurrentState;

                    // A. 显示当前状态的 Banner (类似状态栏)
                    GUIStyle bannerStyle = new GUIStyle(GUI.skin.box);
                    bannerStyle.alignment = TextAnchor.MiddleCenter;
                    bannerStyle.fontStyle = FontStyle.Bold;
                    bannerStyle.normal.textColor = Color.white;
                    bannerStyle.fontSize = 12;

                    // 根据状态改变颜色
                    if (currentState == GameState.BoardMode)
                    {
                        GUI.backgroundColor = new Color(0, 0.6f, 1f); // 蓝色背景
                        GUILayout.Box($"CURRENT STATE: {currentState} (Turn Based)", bannerStyle, GUILayout.ExpandWidth(true), GUILayout.Height(25));
                    }
                    else
                    {
                        GUI.backgroundColor = Color.gray; // 灰色背景
                        GUILayout.Box($"CURRENT STATE: {currentState} (Realtime)", bannerStyle, GUILayout.ExpandWidth(true), GUILayout.Height(25));
                    }
                    GUI.backgroundColor = Color.white; // 恢复颜色

                    GUILayout.Space(5);

                    // B. 状态切换大按钮
                    if (currentState != GameState.BoardMode)
                    {
                        // 还没进大富翁模式：显示绿色“进入”按钮
                        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); 
                        if (GUILayout.Button("🚀 ENTER Board Mode", GUILayout.Height(35)))
                        {
                            gameManager.ChangeState(GameState.BoardMode);
                        }
                    }
                    else 
                    {
                        // 已经在模式中：显示红色“退出”按钮
                        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); 
                        if (GUILayout.Button("❌ EXIT to Free Roam", GUILayout.Height(35)))
                        {
                            gameManager.ChangeState(GameState.FreeRoam);
                        }
                    }
                    
                    GUI.backgroundColor = Color.white; // 记得恢复颜色
                }
                else
                {
                    EditorGUILayout.HelpBox("⚠️ GameManager Instance missing!", MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Run the game (Play Mode) to switch states.", MessageType.Info);
            }
            
            // ==================== 🎲 调试操作区 ====================
            GUILayout.Space(15);
            EditorGUILayout.LabelField("🎲 Debug Actions", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            
            // C. 智能掷骰子按钮 (只在 BoardMode 下亮起)
            bool isBoardMode = Application.isPlaying && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.BoardMode;
            
            GUI.enabled = isBoardMode; // 如果不是 BoardMode，禁用此按钮(变灰)
            GUI.backgroundColor = isBoardMode ? Color.green : Color.white;
            
            if (GUILayout.Button("🎲 Roll Dice", GUILayout.Height(40)))
            {
                manager.RequestRollDice();
            }
            
            GUI.enabled = true; // 恢复全局启用状态
            GUI.backgroundColor = Color.white;

            // D. 重置按钮 (随时可用)
            if (GUILayout.Button("🔄 Reset Pos", GUILayout.Height(40)))
            {
                if (Application.isPlaying)
                {
                    manager.ResetToStart();
                }
            }
            
            GUILayout.EndHorizontal();
            
            // 提示信息
            if (!isBoardMode && Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Please 'ENTER Board Mode' to enable Dice Rolling.", MessageType.Warning);
            }
        }
    }
}
