using IndieGame.Gameplay.ActionPoint;
using IndieGame.Gameplay.Board.FogOfWar;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Crafting;
using IndieGame.Gameplay.Date;
using IndieGame.Gameplay.Dialogue;
using IndieGame.Gameplay.Economy;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Shop;
using IndieGame.Gameplay.Town;

namespace IndieGame.Core
{
    /// <summary>
    /// 全局游戏服务注册中心（静态门面）：
    /// <para>
    /// 集中提供对核心 Manager / System 单例的访问入口，让调用方不必直接写
    /// <c>XxxManager.Instance.YYY</c>。每个服务属性默认透传到对应单例的 Instance，
    /// 但允许通过 <c>OverrideXxx</c> 方法在测试或特殊场景中替换实例。
    /// </para>
    /// <para>
    /// 设计目标：
    /// 1) 作为"未来用接口替换具体实现"的过渡层 —— 后续可把 GoldSystem 等替换为
    ///    IGoldSystem 接口，调用方无需大规模改写；
    /// 2) 提供一处可见的"核心系统索引"，新人通过本类即可了解项目主要服务集合；
    /// 3) 不强求一次性迁移所有 39+ 个 <c>.Instance</c> 调用点，调用方可逐步迁移。
    /// </para>
    /// <para>
    /// 注意：本类**不创建**任何单例，只是访问已存在的实例。Manager 的实例化仍由
    /// GameBootstrapper 负责。在 Manager 未就绪时（如启动早期或场景切换瞬间），
    /// 调用方仍需做 null 检查。
    /// </para>
    /// </summary>
    public static class GameServices
    {
        // 各服务的"覆盖实例"。为 null 时回落到对应 Manager.Instance；
        // 不为 null 时返回该实例（便于测试或特殊场景注入）。
        private static GoldSystem _goldOverride;
        private static ActionPointSystem _actionPointOverride;
        private static InventoryManager _inventoryOverride;
        private static CraftingSystem _craftingOverride;
        private static ShopSystem _shopOverride;
        private static DialogueManager _dialogueOverride;
        private static DateSystem _dateOverride;
        private static TownUnlockManager _townUnlockOverride;
        private static BoardMapManager _boardMapOverride;
        private static BoardEntityManager _boardEntityOverride;
        private static FogOfWarManager _fogOfWarOverride;

        /// <summary> 金币系统。 </summary>
        public static GoldSystem Gold => _goldOverride != null ? _goldOverride : GoldSystem.Instance;

        /// <summary> 行动点系统。 </summary>
        public static ActionPointSystem ActionPoint => _actionPointOverride != null ? _actionPointOverride : ActionPointSystem.Instance;

        /// <summary> 背包管理器。 </summary>
        public static InventoryManager Inventory => _inventoryOverride != null ? _inventoryOverride : InventoryManager.Instance;

        /// <summary> 打造系统。 </summary>
        public static CraftingSystem Crafting => _craftingOverride != null ? _craftingOverride : CraftingSystem.Instance;

        /// <summary> 商店系统。 </summary>
        public static ShopSystem Shop => _shopOverride != null ? _shopOverride : ShopSystem.Instance;

        /// <summary> 对话管理器。 </summary>
        public static DialogueManager Dialogue => _dialogueOverride != null ? _dialogueOverride : DialogueManager.Instance;

        /// <summary> 日期系统。 </summary>
        public static DateSystem Date => _dateOverride != null ? _dateOverride : DateSystem.Instance;

        /// <summary> 城镇解锁管理器。 </summary>
        public static TownUnlockManager TownUnlock => _townUnlockOverride != null ? _townUnlockOverride : TownUnlockManager.Instance;

        /// <summary> 棋盘地图节点缓存。 </summary>
        public static BoardMapManager BoardMap => _boardMapOverride != null ? _boardMapOverride : BoardMapManager.Instance;

        /// <summary> 棋盘实体管理器。 </summary>
        public static BoardEntityManager BoardEntity => _boardEntityOverride != null ? _boardEntityOverride : BoardEntityManager.Instance;

        /// <summary> 战争迷雾管理器。 </summary>
        public static FogOfWarManager FogOfWar => _fogOfWarOverride != null ? _fogOfWarOverride : FogOfWarManager.Instance;

        // ====== 覆盖注入接口（仅供测试或特殊场景使用） ======
        // 传入 null 表示恢复到默认 Manager.Instance 行为。

        public static void OverrideGold(GoldSystem instance) => _goldOverride = instance;
        public static void OverrideActionPoint(ActionPointSystem instance) => _actionPointOverride = instance;
        public static void OverrideInventory(InventoryManager instance) => _inventoryOverride = instance;
        public static void OverrideCrafting(CraftingSystem instance) => _craftingOverride = instance;
        public static void OverrideShop(ShopSystem instance) => _shopOverride = instance;
        public static void OverrideDialogue(DialogueManager instance) => _dialogueOverride = instance;
        public static void OverrideDate(DateSystem instance) => _dateOverride = instance;
        public static void OverrideTownUnlock(TownUnlockManager instance) => _townUnlockOverride = instance;
        public static void OverrideBoardMap(BoardMapManager instance) => _boardMapOverride = instance;
        public static void OverrideBoardEntity(BoardEntityManager instance) => _boardEntityOverride = instance;
        public static void OverrideFogOfWar(FogOfWarManager instance) => _fogOfWarOverride = instance;

        /// <summary>
        /// 一次性清空所有覆盖实例，恢复到"全部透传 Manager.Instance"的默认状态。
        /// 主要供单元测试在 TearDown 阶段调用，避免覆盖泄漏到下一个测试。
        /// </summary>
        public static void ClearAllOverrides()
        {
            _goldOverride = null;
            _actionPointOverride = null;
            _inventoryOverride = null;
            _craftingOverride = null;
            _shopOverride = null;
            _dialogueOverride = null;
            _dateOverride = null;
            _townUnlockOverride = null;
            _boardMapOverride = null;
            _boardEntityOverride = null;
            _fogOfWarOverride = null;
        }
    }
}
