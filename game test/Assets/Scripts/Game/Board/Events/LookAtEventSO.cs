using System.Collections;
using IndieGame.Gameplay.Board.Runtime;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Events
{
    [CreateAssetMenu(menuName = "IndieGame/Board/Events/Player Look At")]
    public class LookAtEventSO : BoardEventSO
    {
        public float duration = 0.5f;

        public override IEnumerator Execute(BoardGameManager manager, Transform targetContext)
        {
            if (targetContext == null) yield break;

            Transform player = manager.movementController.playerToken;
            Quaternion targetRot = Quaternion.LookRotation(targetContext.position - player.position);
            
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                // 平滑插值转向目标
                player.rotation = Quaternion.Slerp(player.rotation, targetRot, timer * 5f);
                yield return null;
            }
        }
    }
}
