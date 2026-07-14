using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Dialogue;

namespace IndieGame.Core
{
    // UI 业务事件：背包、打造、商店、露营/城镇、系统菜单、图鉴、装备与改名弹窗
    // （L5 重构：原 GameEvents.cs 单文件 1000+ 行，按领域拆分为 GameEvents.*.cs 多文件，
    // 命名空间与全部类型定义保持不变，纯文件级重组。）
    /// <summary>
    /// 打开背包事件：
    /// 由 UI 或输入系统触发，InventoryManager 监听。
    /// </summary>
    public struct OpenInventoryEvent
    {
    }

    /// <summary>
    /// 关闭背包事件：
    /// 由背包关闭按钮或 ESC 触发，InventoryFullScreenController 监听。
    /// </summary>
    public struct CloseInventoryEvent
    {
    }

    /// <summary>
    /// 背包已打开事件（状态通知）：
    /// 由 InventoryManager 在执行 OpenInventory() 后广播，与命令式的 <see cref="OpenInventoryEvent"/> 严格区分：
    /// - OpenInventoryEvent：请求 Manager 打开背包；
    /// - InventoryOpenedEvent：通知所有订阅方"背包已打开"，UI 据此做动画/状态变化。
    /// </summary>
    public struct InventoryOpenedEvent
    {
    }

    /// <summary>
    /// 背包已关闭事件（状态通知）：
    /// 由 InventoryManager 在执行 CloseInventory() 后广播。与 <see cref="CloseInventoryEvent"/> 的区别同上。
    /// </summary>
    public struct InventoryClosedEvent
    {
    }

    /// <summary>
    /// 背包数据变更事件：
    /// 约定用于“依赖背包数量的系统”（例如打造系统）刷新可执行状态。
    /// </summary>
    public struct OnInventoryChanged
    {
        // 当前背包槽位快照（引用类型；监听方只读使用，不应修改）
        public IReadOnlyList<InventorySlot> Slots;
    }

    /// <summary>
    /// 图纸消耗完成事件：
    /// 打造系统在一次成功制造后广播，用于 UI 与其他系统解耦联动。
    /// </summary>
    public struct OnBlueprintConsumed
    {
        // 被消耗的图纸 ID
        public string BlueprintID;
    }

    /// <summary>
    /// 打造图纸槽位点击事件：
    /// 由 BlueprintSlotUI 点击后广播，CraftingUIController 监听并切换选中态。
    /// </summary>
    public struct CraftBlueprintSlotClickedEvent
    {
        // 被点击的列表条目唯一键（用于精确定位当前条目）
        public string EntryKey;
        // 被点击的图纸 ID
        public string BlueprintID;
    }

    /// <summary>
    /// 打开打造界面事件：
    /// 由业务入口（如 Camp 按钮）发起，CraftingUIController 监听后执行显示逻辑。
    /// </summary>
    public struct OpenCraftingUIEvent
    {
    }

    /// <summary>
    /// 关闭打造界面事件：
    /// 由 ESC/Cancel 或外部系统发起，CraftingUIController 监听后执行隐藏逻辑。
    /// </summary>
    public struct CloseCraftingUIEvent
    {
    }

    /// <summary>
    /// 打开商店界面请求事件：
    /// 由“商人 NPC 交互入口”等业务层发起，ShopUIController 监听后执行显示与列表构建。
    ///
    /// 字段设计说明：
    /// 1) ShopID：用于在 ShopSystem 中定位具体商店配置；
    /// 2) Interactor：发起交互对象（通常是玩家），便于后续做镜头/朝向/音效扩展；
    /// 3) Merchant：被交互的商人对象（可选），用于后续扩展“商人头像/名称”等 UI 信息。
    /// </summary>
    public struct OpenShopUIRequestEvent
    {
        // 商店唯一 ID（必须能映射到 ShopSO）
        public string ShopID;
        // 发起交互对象（通常是玩家）
        public GameObject Interactor;
        // 商人对象（可选）
        public GameObject Merchant;
    }

    /// <summary>
    /// 关闭商店界面请求事件：
    /// 统一由 ESC/Cancel 或外部流程发起，ShopUIController 监听后执行收尾与隐藏。
    /// </summary>
    public struct CloseShopUIRequestEvent
    {
    }

    /// <summary>
    /// 商店列表项点击事件：
    /// 由 ShopItemSlotUI 点击后广播，ShopUIController 监听并切换选中商品。
    /// </summary>
    public struct ShopItemSlotClickedEvent
    {
        // 被点击条目的唯一键（用于 UI 层精确定位）
        public string EntryKey;
        // 所属商店 ID
        public string ShopID;
        // 商店条目 ID（不是 ItemSO.ID，允许同物品多条不同定价/规则）
        public string ShopEntryID;
    }

    /// <summary>
    /// 商店购买成功事件：
    /// 由 ShopSystem 在一次交易完成后广播，供提示 UI、音效、任务系统等解耦监听。
    /// </summary>
    public struct ShopPurchaseCompletedEvent
    {
        // 商店 ID
        public string ShopID;
        // 商店条目 ID
        public string ShopEntryID;
        // 实际购买数量
        public int Quantity;
        // 实际总花费
        public int TotalCost;
    }

    /// <summary>
    /// 请求打开“自定义成品名称”输入弹窗事件：
    /// CraftingUIController 在点击制造按钮后发布该事件，
    /// 由专门的输入弹窗 UI 监听并展示输入框。
    /// </summary>
    public struct CraftNameInputPopupRequestEvent
    {
        // 请求唯一 ID（用于响应时匹配，防止并发串线）
        public int RequestId;
        // 本次制造对应的图纸 ID（便于弹窗层按需展示额外信息）
        public string BlueprintID;
        // 输入框默认文本（通常是产出物原始名称）
        public string DefaultName;
    }

    /// <summary>
    /// “自定义成品名称”输入弹窗响应事件：
    /// 输入弹窗点击确认/取消后，通过该事件把结果回传给 CraftingUIController。
    /// </summary>
    public struct CraftNameInputPopupResultEvent
    {
        // 对应请求 ID
        public int RequestId;
        // 是否确认
        public bool Confirmed;
        // 用户输入的自定义名称（取消时可为空）
        public string CustomName;
    }

    /// <summary>
    /// 制造历史新增事件：
    /// 每次成功制造后，CraftingSystem 广播该事件，供 UI（复现 Tab）刷新列表。
    /// </summary>
    public struct CraftHistoryRecordedEvent
    {
        // 历史记录对应的图纸 ID
        public string BlueprintID;
        // 历史记录里的最终名称（玩家确认后的名称）
        public string CustomName;
    }
    /// <summary>
    /// 露营"睡觉"业务请求事件：
    /// 由 CampUIView 在玩家点击 Sleep 按钮后发布，CampUIController 监听后执行完整流程
    /// （黑屏 → 恢复行动点 → 推进日期 → 自动存档 → 隐藏菜单 → 返回棋盘）。
    /// 设计意图：把"睡觉"这一业务编排从 View 中分离出去，避免 View 直接调用系统级 API。
    /// </summary>
    public struct CampSleepRequestedEvent { }

    // ===================== 城镇 UI 事件 =====================

    /// <summary>
    /// 城镇动作按钮 Hover 事件：
    /// 由 TownActionButton 在鼠标进入时触发。
    /// </summary>
    public struct TownActionButtonHoverEvent
    {
        public int Index;
    }

    /// <summary>
    /// 城镇动作按钮 Click 事件：
    /// 由 TownActionButton 在点击时触发。
    /// </summary>
    public struct TownActionButtonClickEvent
    {
        public int Index;
    }

    /// <summary>
    /// 城镇动作按钮 Exit 事件：
    /// 由 TownActionButton 在鼠标移出时触发。
    /// </summary>
    public struct TownActionButtonExitEvent
    {
        public int Index;
    }

    /// <summary>
    /// 离开城镇请求事件：
    /// 由 TownUIView 在玩家点击”离开”时触发，TownState 监听后切回玩家回合。
    /// </summary>
    public struct TownLeaveRequestedEvent { }

    /// <summary>
    /// 旅馆住宿业务请求事件：
    /// 由 TownUIView 在玩家点击"旅馆"按钮后发布，TownUIController 监听后执行完整流程
    /// （黑屏 → 恢复行动点 → 推进日期 → 自动存档 → 黑屏淡出，菜单留在城镇）。
    /// 设计意图：把跨系统业务编排从 View 中剥离出去，由 Controller 集中管理。
    /// </summary>
    public struct InnSleepRequestedEvent { }

    /// <summary>
    /// 城镇传送业务请求事件：
    /// 由 TownUIView 在玩家点击"传送"按钮后发布，TownUIController 监听后接管整个传送流程
    /// （显示选单 → 等待选择 → 黑屏 → 移动玩家 → 同步相机 → 切换背景 → 黑屏淡出）。
    /// </summary>
    public struct TownTeleportRequestedEvent { }
    /// <summary>
    /// 系统菜单打开通知事件：
    /// SystemMenuController 在面板弹出时广播，供其他系统感知（如音效、暂停逻辑等）。
    /// </summary>
    public struct SystemMenuOpenedEvent { }

    /// <summary>
    /// 系统菜单关闭通知事件：
    /// SystemMenuController 在面板收起时广播。
    /// </summary>
    public struct SystemMenuClosedEvent { }
    // ── Memory 图鉴系统事件 ────────────────────────────────────────────

    /// <summary>
    /// 打开图鉴界面请求事件：
    /// 由 CampUIView 在玩家点击 Memory 按钮时发布，MemoryUIController 监听后执行显示逻辑。
    /// </summary>
    public struct OpenMemoryUIEvent { }

    /// <summary>
    /// 关闭图鉴界面请求事件：
    /// 由关闭按钮发起，MemoryUIController 监听后执行隐藏逻辑。
    /// </summary>
    public struct CloseMemoryUIEvent { }

    /// <summary>
    /// 图鉴界面已打开通知事件：
    /// MemoryUIController 在面板显示后广播。
    /// </summary>
    public struct MemoryUIOpenedEvent { }

    /// <summary>
    /// 图鉴界面已关闭通知事件：
    /// MemoryUIController 在面板隐藏后广播。
    /// </summary>
    public struct MemoryUIClosedEvent { }

    /// <summary>
    /// 图纸获得事件：
    /// 玩家通过任何途径（剧情触发、捡取、购买）获得一张图纸时广播。
    /// MemorySystem 监听此事件以追踪"至今获得的所有图纸"，包含已消耗项。
    /// </summary>
    public struct BlueprintObtainedEvent
    {
        // 获得的图纸 ID（与 BlueprintSO.ID 对应）
        public string BlueprintID;
    }

    /// <summary>
    /// Memory 列表项点击事件：
    /// 由 MemorySlotUI 点击后广播，MemoryUIController 监听并切换详情面板。
    /// </summary>
    public struct MemorySlotClickedEvent
    {
        // 列表条目唯一 Key（非业务 ID，用于 ListManager 内部索引）
        public string EntryKey;
    }

    /// <summary>
    /// 武器装备事件：
    /// WeaponEquipController.Equip 成功后广播，UI（属性面板/武器面板）据此刷新，不直接引用 WeaponEquipController。
    /// </summary>
    public struct WeaponEquippedEvent
    {
        // 装备者
        public GameObject Owner;
        // 装备的武器配置
        public WeaponSO Weapon;
    }

    /// <summary>
    /// 武器卸下事件：
    /// WeaponEquipController.Unequip 成功后广播。
    /// </summary>
    public struct WeaponUnequippedEvent
    {
        // 装备者
        public GameObject Owner;
        // 被卸下的武器配置
        public WeaponSO Weapon;
    }

    /// <summary>
    /// 槽位改名弹窗请求事件：
    /// 背包/强化详情面板点击"改名"按钮后广播，与 CraftNameInputPopupRequestEvent 语义区分
    /// （后者专属"打造确认流程"，这里只是通用的"给槽位换个名字"，不触发任何制造逻辑）。
    /// </summary>
    public struct RenameSlotPopupRequestEvent
    {
        // 请求 ID，用于匹配响应（避免连续点击串线）
        public int RequestId;
        // 输入框默认值（当前名称）
        public string DefaultName;
    }

    /// <summary>
    /// 槽位改名弹窗结果事件。
    /// </summary>
    public struct RenameSlotPopupResultEvent
    {
        public int RequestId;
        public bool Confirmed;
        public string CustomName;
    }

    /// <summary>
    /// 防具装备事件：
    /// ArmorEquipController.Equip 成功后广播，UI（属性面板/装备面板）据此刷新，不直接引用 ArmorEquipController。
    /// </summary>
    public struct ArmorEquippedEvent
    {
        // 装备者
        public GameObject Owner;
        // 装备的防具配置
        public ArmorSO Armor;
    }

    /// <summary>
    /// 防具卸下事件：
    /// ArmorEquipController.Unequip 成功后广播。
    /// </summary>
    public struct ArmorUnequippedEvent
    {
        // 装备者
        public GameObject Owner;
        // 被卸下的防具配置
        public ArmorSO Armor;
    }

    /// <summary>
    /// 打开装备界面事件：
    /// 由 UI 或输入系统触发，EquipmentUIController 监听。
    /// </summary>
    public struct OpenEquipmentUIEvent { }

    /// <summary>
    /// 关闭装备界面事件：
    /// 由装备界面关闭按钮或 ESC 触发，EquipmentUIController 监听。
    /// </summary>
    public struct CloseEquipmentUIEvent { }

    /// <summary>
    /// 装备界面已打开事件（状态通知）：
    /// 由 EquipmentUIController 在处理完 OpenEquipmentUIEvent 后广播，供数值面板等复用组件据此刷新。
    /// </summary>
    public struct EquipmentUIOpenedEvent { }

    /// <summary>
    /// 装备界面已关闭事件（状态通知）：
    /// 由 EquipmentUIController 在处理完 CloseEquipmentUIEvent 后广播。
    /// </summary>
    public struct EquipmentUIClosedEvent { }
}
