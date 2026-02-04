using UnityEngine;
using IndieGame.Core;

namespace IndieGame.Gameplay.Board.Runtime.States
{
    /// <summary>
    /// 露营状态：
    /// 进入时执行黑屏转场并加载 Camping 场景，完成后显示露营 UI。
    /// 退出时同样执行黑屏转场并返回上一个场景。
    /// </summary>
    public class CampingState : BoardState
    {
        private readonly LocationID _campingLocationId;
        private readonly string _campingSceneName;

        public CampingState(LocationID campingLocationId, string campingSceneName = "Camp")
        {
            _campingLocationId = campingLocationId;
            _campingSceneName = campingSceneName;
        }

        public override void OnEnter(BoardGameManager context)
        {
            if (context == null || !context.isActiveAndEnabled) return;
            // 进入露营：交由 SceneLoader 负责淡入淡出与加载流程
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(_campingSceneName, _campingLocationId, 1f);
            }
        }

        public override void OnExit(BoardGameManager context)
        {
            if (context == null || !context.isActiveAndEnabled) return;
            // 按需求：不做场景跳转
        }
    }
}
