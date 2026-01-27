using UnityEngine;

public class UIPriorityRoots : MonoBehaviour
{
    [Header("UI Layer Roots")]
    [SerializeField] private Transform screenOverlayTop75;
    [SerializeField] private Transform screenCameraBottom25;

    public Transform OverlayTop75 => screenOverlayTop75;
    public Transform CameraBottom25 => screenCameraBottom25;
}
