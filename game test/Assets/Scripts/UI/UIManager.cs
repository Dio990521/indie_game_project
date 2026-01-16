using UnityEngine;
using UnityEngine.UI;
using IndieGame.Core.Utilities;

namespace IndieGame.UI
{
    public enum UILayerPriority
    {
        Bottom25,
        Top75
    }

    public class UIManager : MonoSingleton<UIManager>
    {
        [Header("UI Roots")]
        [SerializeField] private Transform screenOverlayTop75;
        [SerializeField] private Transform screenCameraBottom25;

        protected override void Awake()
        {
            base.Awake();
            EnsureRoots();
        }

        public Transform GetRoot(UILayerPriority priority)
        {
            switch (priority)
            {
                case UILayerPriority.Top75:
                    return screenOverlayTop75 != null
                        ? screenOverlayTop75
                        : FindRootByName("UIScreenOverlay_TOP75");
                case UILayerPriority.Bottom25:
                default:
                    return screenCameraBottom25 != null
                        ? screenCameraBottom25
                        : FindRootByName("UIScreenCamera_Bottom25");
            }
        }

        public T SpawnOnLayer<T>(T prefab, UILayerPriority priority) where T : Component
        {
            Transform root = GetRoot(priority);
            if (root == null)
            {
                Debug.LogWarning("[UIManager] Missing UI root for layer.");
                return null;
            }

            return Instantiate(prefab, root);
        }

        public void AttachToLayer(Transform uiRoot, UILayerPriority priority)
        {
            Transform root = GetRoot(priority);
            if (root == null || uiRoot == null) return;
            uiRoot.SetParent(root, false);
        }

        private Transform FindRootByName(string rootName)
        {
            GameObject root = GameObject.Find(rootName);
            return root != null ? root.transform : null;
        }

        private void EnsureRoots()
        {
            if (screenOverlayTop75 == null)
            {
                screenOverlayTop75 = CreateRootCanvas("UIScreenOverlay_TOP75", RenderMode.ScreenSpaceOverlay);
            }

            if (screenCameraBottom25 == null)
            {
                screenCameraBottom25 = CreateRootCanvas("UIScreenCamera_Bottom25", RenderMode.ScreenSpaceCamera);
                var canvas = screenCameraBottom25.GetComponent<Canvas>();
                if (canvas != null && canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }
            }
        }

        private Transform CreateRootCanvas(string name, RenderMode mode)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = mode;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return go.transform;
        }
    }
}
