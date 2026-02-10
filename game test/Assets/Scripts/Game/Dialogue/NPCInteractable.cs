using UnityEngine;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 通用交互接口：
    /// 任何可被玩家触发交互的对象都可实现该接口。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 执行交互。
        /// </summary>
        /// <param name="interactor">发起交互的对象（通常是玩家）</param>
        void Interact(GameObject interactor);
    }

    /// <summary>
    /// NPC 交互组件：
    /// 负责把“玩家交互行为”转换为“启动对话”请求。
    ///
    /// 调用来源说明：
    /// - 正常流程下由 PlayerInteractionController 在玩家按下 Interact 且检测到该 NPC 为当前目标时调用。
    /// - 本组件不自行监听输入，保持职责单一。
    /// </summary>
    public class NPCInteractable : MonoBehaviour, IInteractable
    {
        [Header("Dialogue")]
        [Tooltip("该 NPC 触发的对话资源")]
        [SerializeField] private DialogueDataSO dialogueData;

        /// <summary>
        /// 交互入口：
        /// 按需求直接调用 DialogueManager.Instance.StartDialogue(myDialogueData)。
        /// </summary>
        public void Interact(GameObject interactor)
        {
            if (dialogueData == null)
            {
                Debug.LogWarning($"[NPCInteractable] Missing dialogue data on {name}.");
                return;
            }

            DialogueManager manager = DialogueManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[NPCInteractable] DialogueManager instance not found.");
                return;
            }

            manager.StartDialogue(dialogueData);
        }
    }
}
