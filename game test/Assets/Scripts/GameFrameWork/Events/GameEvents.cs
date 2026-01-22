using System;
using IndieGame.Gameplay.Board.Runtime;

namespace IndieGame.Core
{
    public struct PlayerReachedNodeEvent
    {
        public MapWaypoint Node;
    }

    public struct GameStateChangedEvent
    {
        public GameState NewState;
    }

    public struct BoardEntityInteractionEvent
    {
        public Gameplay.Board.Runtime.BoardEntity Player;
        public Gameplay.Board.Runtime.BoardEntity Target;
        public MapWaypoint Node;
        public System.Action OnCompleted;
    }
}
