using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.UI
{
    public enum BoardActionId
    {
        RollDice,
        Item,
        Camp
    }

    public class BoardActionOptionData
    {
        public BoardActionId Id;
        public string Name;
        public Sprite Icon;
    }

    public class BoardActionMenuData
    {
        public readonly List<BoardActionOptionData> Options = new List<BoardActionOptionData>();
    }
}
