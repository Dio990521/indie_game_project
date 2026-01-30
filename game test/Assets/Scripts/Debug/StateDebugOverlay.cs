using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Debugging
{
    public class StateDebugOverlay : MonoBehaviour
    {
        private GUIStyle _labelStyle;

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            // 1. 初始化样式（只做一次）
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 50,
                    normal = { textColor = Color.white }, // 白色在深色背景上最清晰
                    padding = new RectOffset(10, 10, 10, 10),
                    alignment = TextAnchor.UpperLeft
                };
            }

            // 获取数据（保持你原有的逻辑）
            string globalState = GameManager.Instance?.CurrentState.ToString() ?? "None";
            string boardState = BoardGameManager.Instance?.CurrentState?.GetType().Name ?? "None";
            string overlayState = BoardGameManager.Instance?.OverlayState?.GetType().Name ?? "None";
            string text = $"[Global]: {globalState}\n[Board]: {boardState}\n[Overlay]: {overlayState}";

            // 2. 绘制黑色半透明背景框
            // 使用 GUI.Box 配合一个简单的背景色
            GUI.backgroundColor = new Color(0, 0, 0, 0.9f); // 90% 透明度的黑色
            Vector2 size = _labelStyle.CalcSize(new GUIContent(text)); // 自动计算文本所需的宽高
            Rect boxRect = new Rect(10f, 10f, size.x + 20, size.y + 20);

            GUI.Box(boxRect, ""); // 绘制背景
            GUI.Label(boxRect, text, _labelStyle); // 绘制文字
        }
    }
}
