using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 宝具系统：管理玩家当前持有的宝具列表。
    /// 在 Inspector 中预配置初始宝具，运行时可动态增减。
    /// </summary>
    public class TreasureSystem : MonoSingleton<TreasureSystem>
    {
        [Tooltip("玩家初始持有的宝具列表，在 Inspector 中拖入对应的 SO 资源")]
        [SerializeField] private List<TreasureSO> _ownedTreasures = new List<TreasureSO>();

        /// <summary>只读宝具列表，供 UI 层遍历显示</summary>
        public IReadOnlyList<TreasureSO> OwnedTreasures => _ownedTreasures;

        /// <summary>获得一个新宝具</summary>
        public void AddTreasure(TreasureSO treasure)
        {
            if (treasure == null || _ownedTreasures.Contains(treasure)) return;
            _ownedTreasures.Add(treasure);
        }

        /// <summary>移除一个宝具</summary>
        public void RemoveTreasure(TreasureSO treasure)
        {
            if (treasure == null) return;
            _ownedTreasures.Remove(treasure);
        }

        /// <summary>检查是否持有指定 ID 的宝具</summary>
        public bool HasTreasure(string treasureId)
        {
            foreach (var t in _ownedTreasures)
            {
                if (t != null && t.TreasureId == treasureId) return true;
            }
            return false;
        }
    }
}
