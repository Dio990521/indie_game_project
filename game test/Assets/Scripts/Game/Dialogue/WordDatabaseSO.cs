using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 词条数据库（ScriptableObject）：
    /// 存放全量 WordSO 资源列表，供武器强化界面的"语料库"列表按 ID 索引使用。
    /// 设计与 BlueprintDatabaseSO 一致：运行时只做一次列表 -> Dictionary 的索引转换。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Dialogue/Word Database")]
    public class WordDatabaseSO : ScriptableObject
    {
        [Tooltip("词条资源列表（建议在 Inspector 中维护）")]
        [SerializeField] private List<WordSO> words = new List<WordSO>();

        /// <summary> 词条列表（只读暴露） </summary>
        public IReadOnlyList<WordSO> Words => words;
    }
}
