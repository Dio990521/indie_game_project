using TMPro;
using UnityEngine;

namespace IndieGame.UI.Dialogue
{
    /// <summary>
    /// 对话 UI 绑定器（严格 MVB-Binder）：
    /// 仅负责保存 Inspector 引用并提供只读访问器。
    ///
    /// 重要约束：
    /// - Binder 中严禁放业务逻辑、事件监听、协程、动画代码。
    /// - 所有显示控制逻辑必须在 DialogueUIView / DialogueManager 中完成。
    /// </summary>
    public class DialogueUIBinder : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject dialogueContainer;

        [Header("Text")]
        [SerializeField] private TMP_Text contentValText;
        [SerializeField] private TMP_Text nameValText;

        public CanvasGroup CanvasGroup => canvasGroup;
        public GameObject DialogueContainer => dialogueContainer;
        public TMP_Text ContentValText => contentValText;
        public TMP_Text NameValText => nameValText;
    }
}
