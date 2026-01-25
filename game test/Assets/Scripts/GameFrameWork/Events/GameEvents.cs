using System;
using UnityEngine;
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

    public struct OpenInventoryEvent
    {
    }

    public struct HealthChangedEvent
    {
        public GameObject Owner;
        public int Current;
        public int Max;
    }

    public struct DeathEvent
    {
        public GameObject Owner;
    }

    public struct LevelChangedEvent
    {
        public GameObject Owner;
        public int Level;
    }

    public struct ExpChangedEvent
    {
        public GameObject Owner;
        public int Current;
        public int Required;
    }

    public struct BoardEntityInteractionEvent
    {
        public Gameplay.Board.Runtime.BoardEntity Player;
        public Gameplay.Board.Runtime.BoardEntity Target;
        public MapWaypoint Node;
        public System.Action OnCompleted;
    }
}
