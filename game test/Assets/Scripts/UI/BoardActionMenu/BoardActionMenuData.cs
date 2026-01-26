using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

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
        public LocalizedString Name;
        public Sprite Icon;
    }

    public class BoardActionMenuData
    {
        public readonly List<BoardActionOptionData> Options = new List<BoardActionOptionData>();
    }
}
