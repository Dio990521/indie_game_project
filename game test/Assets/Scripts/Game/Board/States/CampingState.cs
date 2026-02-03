using System.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.UI;
using IndieGame.UI.Camp;

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
        private Coroutine _routine;

        public CampingState(LocationID campingLocationId, string campingSceneName = "Camp")
        {
            _campingLocationId = campingLocationId;
            _campingSceneName = campingSceneName;
        }

        public override void OnEnter(BoardGameManager context)
        {
            if (context == null || !context.isActiveAndEnabled) return;
            // 进入露营：仅触发黑屏淡入并请求加载 Camp（Additive）
            _routine = context.StartCoroutine(EnterRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (context == null || !context.isActiveAndEnabled) return;
            // 按需求：仅清理协程，不做场景跳转
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator EnterRoutine(BoardGameManager context)
        {
            // 1) 黑屏淡入
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = 1f });
            // 2) 请求加载 Camp 场景（Additive）
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(_campingSceneName, _campingLocationId);
            }
            yield return null;
        }
    }
}
