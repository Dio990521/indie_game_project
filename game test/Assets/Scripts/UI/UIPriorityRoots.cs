using UnityEngine;

/// <summary>
/// UI 层级根节点容器：
/// 持有各渲染层级的根节点 Transform 引用，由 UIManager 在初始化时读取。
///
/// 层级说明（SortingOrder 从低到高）：
/// - CameraBottom25  : ScreenSpaceCamera，世界相机关联 UI
/// - OverlayGameUI   : Sort 10，游戏功能 UI（HUD、背包、商店等）
/// - OverlaySystemUI : Sort 20，系统菜单（语言切换、存读档）
/// - OverlayPopup    : Sort 30，确认弹窗（始终在菜单上方）
/// - OverlayFullscreen: Sort 40，全屏转场遮罩（始终最顶层）
/// </summary>
public class UIPriorityRoots : MonoBehaviour
{
    [Header("UI Layer Roots")]
    // 世界相机关联层（ScreenSpaceCamera）
    [SerializeField] private Transform screenCameraBottom25;
    // 游戏功能 UI 层（ScreenSpaceOverlay, SortingOrder = 10）
    [SerializeField] private Transform overlayGameUI;
    // 系统菜单层（ScreenSpaceOverlay, SortingOrder = 20）
    [SerializeField] private Transform overlaySystemUI;
    // 弹窗确认层（ScreenSpaceOverlay, SortingOrder = 30）
    [SerializeField] private Transform overlayPopup;
    // 全屏遮罩层（ScreenSpaceOverlay, SortingOrder = 40）
    [SerializeField] private Transform overlayFullscreen;

    public Transform CameraBottom25    => screenCameraBottom25;
    public Transform OverlayGameUI     => overlayGameUI;
    public Transform OverlaySystemUI   => overlaySystemUI;
    public Transform OverlayPopup      => overlayPopup;
    public Transform OverlayFullscreen => overlayFullscreen;
}
