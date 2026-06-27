using System;
using System.Collections.Generic;
using IndieGame.Gameplay.Crafting;

namespace IndieGame.Gameplay.Equipment
{
    /// <summary>
    /// 一级强化/重铸所需的材料消耗：
    /// 复用 BlueprintRequirement（ItemSO+Amount），一级可以是多种材料组合。
    /// </summary>
    [Serializable]
    public class WeaponEnhanceCostStep
    {
        public List<BlueprintRequirement> Materials = new List<BlueprintRequirement>();
    }
}
