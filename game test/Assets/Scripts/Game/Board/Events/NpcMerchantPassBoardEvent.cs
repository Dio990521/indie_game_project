using System;
using System.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Dialogue;
using IndieGame.Gameplay.Shop;
using IndieGame.UI.Confirmation;

namespace IndieGame.Gameplay.Board.Events
{
    /// <summary>
    /// 路途经过NPC商人时触发的棋盘事件。
    /// 流程：朝向NPC → 触发对话 → 弹出"是否开启商店"选择 → 无论选哪个，事件结束后移动控制器自动继续剩余步数。
    /// 在 WaypointConnection.events 中配置：progressPoint=NPC在曲线上的位置比例，contextTarget=NPC的Transform。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Board/Events/NPC Merchant Pass")]
    public class NpcMerchantPassBoardEvent : BoardEventSO
    {
        [SerializeField] private DialogueDataSO dialogueData;
        [SerializeField] private ShopSO shopData;
        [SerializeField] private float lookAtDuration = 0.5f;

        public override IEnumerator Execute(BoardGameManager manager, Transform npcTransform)
        {
            // 1. 玩家朝向NPC
            yield return LookAt(manager.movementController.playerToken, npcTransform, lookAtDuration);

            // 2. 触发对话并等待结束
            if (dialogueData != null)
            {
                bool dialogueDone = false;
                Action<DialogueEndedEvent> onEnd = _ => dialogueDone = true;
                EventBus.Subscribe<DialogueEndedEvent>(onEnd);
                DialogueManager.Instance.StartDialogue(dialogueData);
                yield return new WaitUntil(() => dialogueDone);
                EventBus.Unsubscribe<DialogueEndedEvent>(onEnd);
            }

            // 3. 弹出是否开店选择
            if (shopData == null) yield break;

            bool choiceMade = false;
            bool openShop = false;
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message   = "是否前往商店？",
                OnConfirm = () => { openShop = true; choiceMade = true; },
                OnCancel  = () => { choiceMade = true; }
            });
            yield return new WaitUntil(() => choiceMade);

            // 4. 开启商店并等待玩家关闭
            if (openShop)
            {
                bool shopClosed = false;
                Action<CloseShopUIRequestEvent> onClose = _ => shopClosed = true;
                EventBus.Subscribe<CloseShopUIRequestEvent>(onClose);
                EventBus.Raise(new OpenShopUIRequestEvent
                {
                    ShopID     = shopData.ID,
                    Interactor = manager.movementController.playerToken.gameObject,
                    Merchant   = npcTransform != null ? npcTransform.gameObject : null
                });
                yield return new WaitUntil(() => shopClosed);
                EventBus.Unsubscribe<CloseShopUIRequestEvent>(onClose);
            }

            // Execute() 返回后，MoveAlongConnection 协程自动恢复剩余步数的移动
        }

        /// <summary>
        /// 平滑转向目标，逻辑与 LookAtEventSO 保持一致。
        /// </summary>
        private IEnumerator LookAt(Transform self, Transform target, float duration)
        {
            if (self == null || target == null) yield break;

            Quaternion targetRot = Quaternion.LookRotation(target.position - self.position);
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                self.rotation = Quaternion.Slerp(self.rotation, targetRot, timer * 5f);
                yield return null;
            }
        }
    }
}
