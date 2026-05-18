using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Board.RandomEvent
{
    /// <summary>
    /// 随机事件数据库：
    /// 集中管理所有可抽取的随机事件，在 Inspector 中配置后挂载到 RandomEventTile。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Events/Random Event Database")]
    public class RandomEventDatabaseSO : ScriptableObject
    {
        [Tooltip("所有可抽取的随机事件列表（正面与负面混合）")]
        public List<RandomEventSO> events = new();
    }
}
