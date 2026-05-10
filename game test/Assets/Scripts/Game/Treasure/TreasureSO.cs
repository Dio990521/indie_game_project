using UnityEngine;
using UnityEngine.Localization;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 宝具数据基类：所有宝具共享的通用字段定义。
    /// 具体宝具继承此类并添加专属字段。
    /// </summary>
    public abstract class TreasureSO : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("全局唯一 ID，与 TreasureItemSelectedEvent.TreasureId 对应")]
        public string TreasureId;

        [Header("UI 显示")]
        [Tooltip("本地化显示名称，显示在宝具列表每行的中间")]
        public LocalizedString DisplayName;

        [Tooltip("显示在列表左侧的宝具图标")]
        public Sprite Icon;

        [Header("规则")]
        [Tooltip("使用此宝具消耗的行动点数量")]
        public int ActionPointCost = 1;
    }
}
