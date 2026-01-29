using System.Collections;
using IndieGame.Gameplay.Board.Runtime;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Events
{
    [CreateAssetMenu(menuName = "IndieGame/Board/Events/Debug Log")]
    public class DebugLogEventSO : BoardEventSO
    {
        public string message;
        public Color logColor = Color.yellow;

        public override IEnumerator Execute(BoardGameManager manager, Transform targetContext)
        {
            // targetContext 可以是场景里配置的触发点物体
            string targetName = targetContext != null ? targetContext.name : "null";
            // 统一用富文本颜色输出，便于调试区分
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(logColor)}>⚡ [Event] {message} (At: {targetName})</color>");
            yield return null; // 稍微停一帧
        }
    }
}
