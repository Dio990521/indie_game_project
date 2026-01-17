using System;
using UnityEngine;
using UnityEngine.UI;
using IndieGame.Core.Utilities;
using UnityEngine.SceneManagement;

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

        [Header("UI Prefabs")]
        [SerializeField] private BoardActionMenuView boardActionMenuPrefab;
        [SerializeField] private Inventory.InventoryUIView inventoryPrefab;
        [SerializeField] private Confirmation.ConfirmationPopupView confirmationPrefab;

        public BoardActionMenuView BoardActionMenuInstance { get; private set; }
        public Inventory.InventoryUIView InventoryInstance { get; private set; }
        public Confirmation.ConfirmationPopupView ConfirmationInstance { get; private set; }

        public static event Action OnUIReady;

        protected override bool DestroyOnLoad => true;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            InitUI();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
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

        private void InitUI()
        {
            Transform uiCanvasRoot = FindOrCreateUICanvasRoot();
            if (screenOverlayTop75 == null)
            {
                screenOverlayTop75 = FindRootByName("UIScreenOverlay_TOP75") ?? CreateRootCanvas("UIScreenOverlay_TOP75", RenderMode.ScreenSpaceOverlay, uiCanvasRoot);
            }

            if (screenCameraBottom25 == null)
            {
                screenCameraBottom25 = FindRootByName("UIScreenCamera_Bottom25") ?? CreateRootCanvas("UIScreenCamera_Bottom25", RenderMode.ScreenSpaceCamera, uiCanvasRoot);
            }
            EnsureEventSystem();
            SpawnUI();
            RefreshWorldCamera();
        }

        private Transform CreateRootCanvas(string name, RenderMode mode, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = mode;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            return go.transform;
        }

        private Transform FindOrCreateUICanvasRoot()
        {
            GameObject existing = GameObject.Find("UICanvas");
            if (existing != null)
            {
                Transform gameRoot = FindGameSystemRoot();
                if (gameRoot != null)
                {
                    existing.transform.SetParent(gameRoot, false);
                }
                return existing.transform;
            }
            GameObject root = new GameObject("UICanvas");
            Transform parent = FindGameSystemRoot();
            root.transform.SetParent(parent != null ? parent : transform, false);
            return root.transform;
        }


        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        private void SpawnUI()
        {
            if (boardActionMenuPrefab != null && BoardActionMenuInstance == null)
            {
                BoardActionMenuInstance = SpawnOnLayer(boardActionMenuPrefab, UILayerPriority.Top75);
                if (BoardActionMenuInstance != null) BoardActionMenuInstance.gameObject.SetActive(true);
            }

            if (inventoryPrefab != null && InventoryInstance == null)
            {
                InventoryInstance = SpawnOnLayer(inventoryPrefab, UILayerPriority.Top75);
                if (InventoryInstance != null) InventoryInstance.gameObject.SetActive(true);
            }

            if (confirmationPrefab != null && ConfirmationInstance == null)
            {
                ConfirmationInstance = SpawnOnLayer(confirmationPrefab, UILayerPriority.Top75);
                if (ConfirmationInstance != null) ConfirmationInstance.gameObject.SetActive(true);
            }

            OnUIReady?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
            RefreshWorldCamera();
        }

        private void RefreshWorldCamera()
        {
            if (screenCameraBottom25 == null) return;
            var canvas = screenCameraBottom25.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = Camera.main;
            }
        }

        private Transform FindGameSystemRoot()
        {
            GameObject root = GameObject.Find("[GameSystem]");
            return root != null ? root.transform : null;
        }
    }
}
