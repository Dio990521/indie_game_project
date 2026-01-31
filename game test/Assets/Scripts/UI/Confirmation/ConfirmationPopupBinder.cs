using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace IndieGame.UI.Confirmation
{
    /// <summary>
    /// 确认弹窗绑定器：
    /// 统一收集弹窗 UI 的核心组件引用，供 View 脚本调用。
    /// </summary>
    public class ConfirmationPopupBinder : MonoBehaviour
    {
        // 根面板（用于显示/隐藏整个弹窗）
        [SerializeField] private GameObject rootPanel;
        // 文本标签（显示提示内容）
        [SerializeField] private TMP_Text messageLabel;
        // 确认按钮
        [SerializeField] private Button confirmButton;
        // 取消按钮
        [SerializeField] private Button cancelButton;

        /// <summary> 根面板 </summary>
        public GameObject RootPanel => rootPanel;
        /// <summary> 文本标签 </summary>
        public TMP_Text MessageLabel => messageLabel;
        /// <summary> 确认按钮 </summary>
        public Button ConfirmButton => confirmButton;
        /// <summary> 取消按钮 </summary>
        public Button CancelButton => cancelButton;
    }
}
