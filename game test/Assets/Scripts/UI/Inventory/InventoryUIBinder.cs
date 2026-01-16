using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.Inventory
{
    public class InventoryUIBinder : MonoBehaviour
    {
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private InventorySlotUI slotPrefab;
        [SerializeField] private Button closeButton;

        public GameObject RootPanel => rootPanel;
        public Transform ContentRoot => contentRoot;
        public InventorySlotUI SlotPrefab => slotPrefab;
        public Button CloseButton => closeButton;
    }
}
