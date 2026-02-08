using System.Collections.Generic;
using UnityEngine;

namespace IndieGame.Gameplay.Crafting
{
    /// <summary>
    /// 图纸数据库（ScriptableObject）：
    /// 作为 CraftingSystem 的配置入口，存放全量图纸资源列表。
    ///
    /// 设计目的：
    /// - 运行时只做一次列表 -> Dictionary 的索引转换
    /// - 业务查询全部走 O(1) 的 Dictionary
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Crafting/Blueprint Database")]
    public class BlueprintDatabaseSO : ScriptableObject
    {
        [Tooltip("图纸资源列表（建议在 Inspector 中维护）")]
        [SerializeField] private List<BlueprintSO> blueprints = new List<BlueprintSO>();

        /// <summary> 图纸列表（只读暴露） </summary>
        public IReadOnlyList<BlueprintSO> Blueprints => blueprints;
    }
}
