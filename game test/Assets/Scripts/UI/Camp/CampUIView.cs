using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using IndieGame.Gameplay.Camp;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 视图：
    /// 负责显示/隐藏露营菜单，并在 Show 时做淡入动画。
    /// </summary>
    public class CampUIView : View
    {
        [Header("Binder")]
        // UI 绑定器（负责动态菜单生成）
        [SerializeField] private CampUIBinder binder;

        [Header("Config")]
        // 默认解锁动作（可在 Inspector 中配置）
        [Tooltip("默认解锁的动作列表（可在 Inspector 配置）")]
        [SerializeField] private List<CampActionSO> defaultActions = new List<CampActionSO>();

        // CanvasGroup 控制淡入淡出
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[CampUIView] Missing binder reference.");
                return;
            }
            // 初始化 CanvasGroup
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        /// <summary>
        /// 显示露营 UI，并淡入。
        /// </summary>
        public override void Show()
        {
            Show(defaultActions);
        }

        /// <summary>
        /// 显示并初始化菜单。
        /// </summary>
        public void Show(List<CampActionSO> unlockedActions)
        {
            // 初始化按钮列表
            if (binder != null)
            {
                binder.InitializeMenu(unlockedActions);
            }

            // 触发淡入动画
            StopAllCoroutines();
            StartCoroutine(FadeInRoutine());
        }

        public override void Hide()
        {
            StopAllCoroutines();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        private IEnumerator FadeInRoutine()
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.25f);
            yield return new WaitForSeconds(0.25f);
        }
    }
}
