using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Debugging
{
    /// <summary>
    /// 状态机调试日志：
    /// 监测 GlobalState / BoardState / OverlayState 变化，变化时通过 Debug.Log 输出。
    /// 原 OnGUI 屏幕叠加已移除，改由 Console 日志记录。
    /// </summary>
    public class StateDebugOverlay : MonoBehaviour
    {
        // 上一帧记录的状态字符串，用于比对是否发生变化
        private string _lastGlobalState;
        private string _lastBoardState;
        private string _lastOverlayState;

        private void Update()
        {
            if (!Application.isPlaying) return;

            string globalState = GameManager.Instance?.CurrentState.ToString() ?? "None";
            string boardState = BoardGameManager.Instance?.CurrentState?.GetType().Name ?? "None";
            string overlayState = BoardGameManager.Instance?.OverlayState?.GetType().Name ?? "None";

            // 任意一个状态发生变化时打印一次完整快照
            if (globalState != _lastGlobalState || boardState != _lastBoardState || overlayState != _lastOverlayState)
            {
                _lastGlobalState = globalState;
                _lastBoardState = boardState;
                _lastOverlayState = overlayState;

                Debug.Log($"[StateDebug] Global={globalState} | Board={boardState} | Overlay={overlayState}");
            }
        }
    }
}
