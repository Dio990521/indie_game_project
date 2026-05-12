using UnityEngine.Localization.Settings;

namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// UI 本地化文本快捷获取工具，封装 LocalizationSettings 调用。
    /// 在 Localization 初始化完成后使用（Awake 之后均可安全调用）。
    /// </summary>
    public static class L10n
    {
        // String Table 名称，需与 Unity Localization Tables 编辑器中的 Collection 名称一致
        private const string Table = "UIText";

        /// <summary>获取本地化字符串</summary>
        public static string Get(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);

        /// <summary>获取带格式参数的本地化字符串，使用标准 {0} {1} 占位符</summary>
        public static string Get(string key, params object[] args)
            => string.Format(LocalizationSettings.StringDatabase.GetLocalizedString(Table, key), args);

        /// <summary>
        /// 所有 String Table Key 常量。
        /// 在代码中统一使用这里的常量，避免散落的魔法字符串。
        /// </summary>
        public static class Keys
        {
            // ── 商店 ─────────────────────────────────────────────────────────
            // 表格值示例：
            //   shop_stock_unlimited  → "Stock: Unlimited"
            //   shop_stock_remaining  → "Stock: {0}"
            //   shop_quota_unlimited  → "Purchase Quota: Unlimited"
            //   shop_quota_remaining  → "Remaining Purchase Quota: {0}"
            //   shop_unit_price       → "Unit Price: {0} G"
            //   shop_empty            → "No purchasable items available."
            //   shop_gold             → "Gold: {0}"
            public const string ShopStockUnlimited  = "shop_stock_unlimited";
            public const string ShopStockRemaining  = "shop_stock_remaining";
            public const string ShopQuotaUnlimited  = "shop_quota_unlimited";
            public const string ShopQuotaRemaining  = "shop_quota_remaining";
            public const string ShopUnitPrice       = "shop_unit_price";
            public const string ShopEmpty           = "shop_empty";
            public const string ShopGold            = "shop_gold";

            // ── 背包 ─────────────────────────────────────────────────────────
            // 表格值示例：
            //   inventory_capacity   → "背包容量：{0}/{1}"（中文）/ "Bag: {0}/{1}"（英文）
            public const string InventoryCapacity   = "inventory_capacity";

            // ── 物品通用 ──────────────────────────────────────────────────────
            // 表格值示例：
            //   item_empty           → "Empty"
            //   item_unknown         → "Unknown Item"
            //   item_category_*      → 对应分类显示名
            public const string ItemEmpty               = "item_empty";
            public const string ItemUnknown             = "item_unknown";
            public const string ItemCategoryEquipment   = "item_category_equipment";
            public const string ItemCategoryConsumable  = "item_category_consumable";
            public const string ItemCategoryMaterial    = "item_category_material";
            public const string ItemCategoryQuest       = "item_category_quest";
            public const string ItemCategoryUnknown     = "item_category_unknown";

            // ── 存档 ─────────────────────────────────────────────────────────
            // 表格值示例：
            //   save_no_data         → "No Save Data"
            //   save_unknown_time    → "Unknown Time"
            //   save_unknown_scene   → "Unknown Scene"
            //   save_slot_label      → "Slot {0}\n{1}\nScene: {2}\nPlay: {3}"
            //   save_load_confirm    → "Load Slot {0}?"
            //   save_no_save         → "No Save"
            //   save_unknown         → "Unknown"
            public const string SaveNoData          = "save_no_data";
            public const string SaveUnknownTime     = "save_unknown_time";
            public const string SaveUnknownScene    = "save_unknown_scene";
            public const string SaveSlotLabel       = "save_slot_label";
            public const string SaveLoadConfirm     = "save_load_confirm";
            public const string SaveNoSave          = "save_no_save";
            public const string SaveUnknown         = "save_unknown";

            // ── 制作 ─────────────────────────────────────────────────────────
            // 表格值示例：
            //   craft_invalid_requirement → "Invalid Requirement"
            public const string CraftInvalidRequirement = "craft_invalid_requirement";
        }
    }
}
