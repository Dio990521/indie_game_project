namespace IndieGame.UI
{
    /// <summary>
    /// UI 硬编码文案目录（L2 修复）：
    /// 把散落在各 UI 脚本里的中文字符串集中到一处，作为接入 Unity Localization 之前的过渡层。
    ///
    /// 迁移指南：
    /// 1) 新增 UI 文案一律加在这里，禁止在业务代码里内联字符串；
    /// 2) 后续接入本地化时，把本类字段逐个替换为 LocalizedString 表项即可，
    ///    调用点无需再改（只改本文件）；
    /// 3) 逻辑层（如 ShopSystem.ShopPurchaseResult.Message）返回的中文仅作日志/调试兜底，
    ///    UI 层应优先根据结构化字段（如 ShopPurchaseFailReason）映射本类文案。
    /// </summary>
    public static class UIText
    {
        // ── 背包 ─────────────────────────────────────────────────────────
        /// <summary> 丢弃确认弹窗，{0} = 物品显示名 </summary>
        public const string DiscardConfirmFormat = "确认丢弃 {0} x1？";

        // 物品分类显示名（背包详情面板）
        public const string CategoryEquipment  = "装备";
        public const string CategoryConsumable = "消耗品";
        public const string CategoryMaterial   = "材料";
        public const string CategoryBlueprint  = "图纸";
        public const string CategoryQuest      = "任务道具";
        public const string CategoryUnknown    = "未知";
    }
}
