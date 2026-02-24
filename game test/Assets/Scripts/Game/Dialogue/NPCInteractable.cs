using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Shop;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// NPC 交互类型：
    /// - Dialogue：按交互键启动对话；
    /// - MerchantShop：按交互键直接打开商店界面。
    /// </summary>
    public enum NPCInteractionType
    {
        Dialogue = 0,
        MerchantShop = 1
    }

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
    /// 负责把“玩家交互行为”转换为“对话请求”或“商店请求”。
    ///
    /// 调用来源说明：
    /// - 正常流程下由 PlayerInteractionController 在玩家按下 Interact 且检测到该 NPC 为当前目标时调用。
    /// - 本组件不自行监听输入，保持职责单一。
    /// </summary>
    public class NPCInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [Tooltip("NPC 交互类型：普通对话 / 商人商店。")]
        [SerializeField] private NPCInteractionType interactionType = NPCInteractionType.Dialogue;

        [Header("Dialogue (Dialogue Mode)")]
        [Tooltip("该 NPC 触发的对话资源（仅 Dialogue 模式使用）。")]
        [SerializeField] private DialogueDataSO dialogueData;

        [Header("Shop (Merchant Mode)")]
        [Tooltip("商人对应的商店配置（仅 MerchantShop 模式使用）。")]
        [SerializeField] private ShopSO shopData;

        /// <summary>
        /// 交互入口：
        /// 根据 NPC 交互类型分流到“对话”或“商店”。
        /// </summary>
        public void Interact(GameObject interactor)
        {
            if (interactionType == NPCInteractionType.MerchantShop)
            {
                OpenShop(interactor);
                return;
            }

            StartDialogue();
        }

        /// <summary>
        /// 对话模式执行：
        /// 直接调用 DialogueManager 开启对应对话。
        /// </summary>
        private void StartDialogue()
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

        /// <summary>
        /// 商人模式执行：
        /// 通过 EventBus 发送打开商店请求，让 ShopUIController 自行处理显示与数据刷新。
        /// </summary>
        private void OpenShop(GameObject interactor)
        {
            if (shopData == null || string.IsNullOrWhiteSpace(shopData.ID))
            {
                Debug.LogWarning($"[NPCInteractable] Missing/invalid ShopSO on merchant NPC: {name}");
                return;
            }

            EventBus.Raise(new OpenShopUIRequestEvent
            {
                ShopID = shopData.ID,
                Interactor = interactor,
                Merchant = gameObject
            });
        }
    }
}
