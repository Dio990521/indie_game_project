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
}
