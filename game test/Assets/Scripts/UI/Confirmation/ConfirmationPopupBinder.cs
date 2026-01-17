using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IndieGame.UI.Confirmation
{
    public class ConfirmationPopupBinder : MonoBehaviour
    {
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        public GameObject RootPanel => rootPanel;
        public TMP_Text MessageLabel => messageLabel;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;
    }
}
