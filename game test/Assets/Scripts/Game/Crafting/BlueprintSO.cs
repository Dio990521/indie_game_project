using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Gameplay.Crafting
{
    /// <summary>
    /// 图纸静态配置（ScriptableObject）：
    /// 该资源用于定义“制造配方本体”，包含：
    /// 1) 图纸唯一 ID（用于存档与索引）
    /// 2) 图纸固定显示名（左侧列表直接使用）
    /// 3) 成品与数量
    /// 4) 材料需求列表
    ///
    /// 说明：
    /// - 图纸名称固定，来自 BlueprintSO.DefaultName，不支持在 BlueprintRecord 里自定义。
    /// - UI 右侧详情面板按需求“只显示成品图标，不显示成品名称文本”。
    /// </summary>
    [CreateAssetMenu(menuName = "IndieGame/Crafting/Blueprint")]
    public class BlueprintSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("图纸唯一 ID。请保持稳定且全局唯一，用于 Dictionary 索引与存档恢复。")]
        [SerializeField] private string id;

        [Tooltip("图纸固定名称（左侧列表直接显示该名称）")]
        [SerializeField] private string defaultName = "Unnamed Blueprint";

        [Header("Output")]
        [Tooltip("制造产出物（制造成功后发放到背包）")]
        [SerializeField] private ItemSO productItem;

        [Tooltip("产出数量（最小 1）")]
        [SerializeField] private int productAmount = 1;

        [Tooltip("可选的图纸图标。如果未设置，将回退为产出物图标。")]
        [SerializeField] private Sprite iconOverride;

        [Header("Requirements")]
        [Tooltip("制造所需材料列表")]
        [SerializeField] private List<BlueprintRequirement> requirements = new List<BlueprintRequirement>();

        /// <summary> 图纸 ID（只读） </summary>
        public string ID => id;

        /// <summary> 默认名称（只读） </summary>
        public string DefaultName => string.IsNullOrWhiteSpace(defaultName) ? "Unnamed Blueprint" : defaultName;

        /// <summary> 产出物（只读） </summary>
        public ItemSO ProductItem => productItem;

        /// <summary> 产出数量（只读，自动兜底 >= 1） </summary>
        public int ProductAmount => Mathf.Max(1, productAmount);

        /// <summary> 材料列表（只读引用） </summary>
        public IReadOnlyList<BlueprintRequirement> Requirements => requirements;

        /// <summary>
        /// 获取用于 UI 展示的图标：
        /// - 优先使用图纸自身覆盖图标
        /// - 否则回退为产出物图标
        /// </summary>
        public Sprite GetDisplayIcon()
        {
            if (iconOverride != null) return iconOverride;
            return productItem != null ? productItem.Icon : null;
        }
    }
}
