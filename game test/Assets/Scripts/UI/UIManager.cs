using System;
using UnityEngine;
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
        private Transform screenOverlayTop75;
        private Transform screenCameraBottom25;

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
            ResolveRoots();
            return priority == UILayerPriority.Top75 ? screenOverlayTop75 : screenCameraBottom25;
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

        private void InitUI()
        {
            ResolveRoots();
            SpawnUI();
            RefreshWorldCamera();
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
            RefreshWorldCamera();
        }

        private void RefreshWorldCamera()
        {
            ResolveRoots();
            if (screenCameraBottom25 == null) return;
            var canvas = screenCameraBottom25.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = Camera.main;
            }
        }

        private void ResolveRoots()
        {
            if (screenOverlayTop75 != null && screenCameraBottom25 != null) return;

            GameObject uiCanvas = GameObject.Find("UICanvas");
            if (uiCanvas == null) return;
            Transform overlay = uiCanvas.transform.Find("UIScreenOverlay_TOP75");
            Transform cameraRoot = uiCanvas.transform.Find("UIScreenCamera_Bottom25");
            if (overlay != null) screenOverlayTop75 = overlay;
            if (cameraRoot != null) screenCameraBottom25 = cameraRoot;
        }

    }
}
