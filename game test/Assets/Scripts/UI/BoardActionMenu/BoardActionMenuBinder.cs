using UnityEngine;

namespace IndieGame.UI
{
    public class BoardActionMenuBinder : MonoBehaviour
    {
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private BoardActionButton buttonPrefab;
        [SerializeField] private CanvasGroup canvasGroup;

        public RectTransform RootRect => rootRect;
        public Transform ButtonContainer => buttonContainer;
        public BoardActionButton ButtonPrefab => buttonPrefab;
        public CanvasGroup CanvasGroup => canvasGroup;
    }
}
