using System;
using System.Collections.Generic;

namespace IndieGame.Gameplay.Inventory
{
    /// <summary>
    /// 武器实例的强化数据：
    /// 随武器实例在"背包槽位"与"装备状态"之间搬运，不绑定具体存放位置。
    /// </summary>
    [Serializable]
    public class WeaponInstanceData
    {
        // 已应用的强化前缀（WordSO.ID），按应用顺序排列，最多 5 个，不可重复
        public List<string> AppliedPrefixWordIds = new List<string>();
    }
}
