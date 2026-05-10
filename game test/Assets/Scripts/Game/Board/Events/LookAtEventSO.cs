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
            yield return LookAt(manager.movementController.playerToken, targetContext, duration);
        }
    }
}
