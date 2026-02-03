using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private string _previousSceneName;
        private Coroutine _routine;

        public CampingState(LocationID campingLocationId, string campingSceneName = "Camp")
        {
            _campingLocationId = campingLocationId;
            _campingSceneName = campingSceneName;
        }

        public override void OnEnter(BoardGameManager context)
        {
            if (context == null || !context.isActiveAndEnabled) return;
            // 记录进入露营前的场景，用于退出时返回
            _previousSceneName = SceneManager.GetActiveScene().name;
            _routine = context.StartCoroutine(EnterRoutine(context));
        }

        public override void OnExit(BoardGameManager context)
        {
            if (context == null || !context.isActiveAndEnabled) return;
            if (_routine != null)
            {
                context.StopCoroutine(_routine);
                _routine = null;
            }
            _routine = context.StartCoroutine(ExitRoutine(context));
        }

        private IEnumerator EnterRoutine(BoardGameManager context)
        {
            // 1) 黑屏淡入
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = 1f });
            yield return new WaitForSeconds(1f);

            // 2) 加载 Camping 场景
            if (SceneLoader.Instance != null)
            {
                // 使用 SceneLoader 的统一加载逻辑
                SceneLoader.Instance.LoadScene(_campingSceneName, _campingLocationId);
            }

            // 等待场景切换完成
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == _campingSceneName);

            // 3) 黑屏淡出
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = 1f });
            yield return new WaitForSeconds(1f);

            // 4) 打开露营 UI
            if (UIManager.Instance != null && UIManager.Instance.CampUIInstance != null)
            {
                UIManager.Instance.CampUIInstance.Show();
            }
        }

        private IEnumerator ExitRoutine(BoardGameManager context)
        {
            // 1) 黑屏淡入
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = 1f });
            yield return new WaitForSeconds(1f);

            // 2) 关闭露营 UI
            if (UIManager.Instance != null && UIManager.Instance.CampUIInstance != null)
            {
                UIManager.Instance.CampUIInstance.Hide();
            }

            // 3) 返回上一个场景
            if (SceneLoader.Instance != null && !string.IsNullOrEmpty(_previousSceneName))
            {
                // 返回前一个场景（由 SceneRegistry 决定加载策略）
                SceneLoader.Instance.LoadScene(_previousSceneName, null);
            }

            // 等待场景切换完成
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == _previousSceneName);

            // 4) 黑屏淡出
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = 1f });
            yield return new WaitForSeconds(1f);
        }
    }
}
