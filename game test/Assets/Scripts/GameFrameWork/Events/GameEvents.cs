using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;

namespace IndieGame.Core
{
    /// <summary>
    /// 玩家抵达节点事件：
    /// 棋盘移动到达某个地块时触发。
    /// </summary>
    public struct PlayerReachedNodeEvent
    {
        // 抵达的节点
        public MapWaypoint Node;
    }

    /// <summary>
    /// 游戏状态变更事件：
    /// GameManager.ChangeState 后广播。
    /// </summary>
    public struct GameStateChangedEvent
    {
        // 新的游戏状态
        public GameState NewState;
    }

    /// <summary>
    /// 打开背包事件：
    /// 由 UI 或输入系统触发，InventoryManager 监听。
    /// </summary>
    public struct OpenInventoryEvent
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
    /// 生命值变化事件：
    /// 由 CharacterStats 在受到伤害/治疗时广播。
    /// </summary>
    public struct HealthChangedEvent
    {
        // 归属对象（通常是角色 GameObject）
        public GameObject Owner;
        // 当前生命
        public int Current;
        // 最大生命
        public int Max;
    }

    /// <summary>
    /// 死亡事件：
    /// 生命归零时触发。
    /// </summary>
    public struct DeathEvent
    {
        // 归属对象
        public GameObject Owner;
    }

    /// <summary>
    /// 等级变化事件：
    /// 升级或设置等级时触发。
    /// </summary>
    public struct LevelChangedEvent
    {
        // 归属对象
        public GameObject Owner;
        // 当前等级
        public int Level;
    }

    /// <summary>
    /// 经验变化事件：
    /// 获得经验或升级后触发。
    /// </summary>
    public struct ExpChangedEvent
    {
        // 归属对象
        public GameObject Owner;
        // 当前经验
        public int Current;
        // 下一级所需经验
        public int Required;
    }

    /// <summary>
    /// 场景过渡事件：
    /// SceneLoader 在加载后广播，用于初始化出生点。
    /// </summary>
    public struct SceneTransitionEvent
    {
        // 场景名
        public string SceneName;
        // 目标出生点 ID
        public LocationID TargetLocation;
        // 棋盘返回时的节点索引
        public int WaypointIndex;
        // 是否为返回棋盘流程
        public bool ReturnToBoard;
    }

    /// <summary>
    /// 场景模式变化事件：
    /// SceneLoader 在场景加载完成时广播。
    /// </summary>
    public struct GameModeChangedEvent
    {
        // 场景名
        public string SceneName;
        // 场景模式
        public GameMode Mode;
    }

    /// <summary>
    /// 棋盘实体交互事件：
    /// 到达节点时若检测到其他实体，会触发交互事件。
    /// </summary>
    public struct BoardEntityInteractionEvent
    {
        // 玩家实体
        public Gameplay.Board.Runtime.BoardEntity Player;
        // 被交互的目标实体
        public Gameplay.Board.Runtime.BoardEntity Target;
        // 交互发生的节点
        public MapWaypoint Node;
        // 交互完成回调（必须调用避免流程卡住）
        public System.Action OnCompleted;
    }

    /// <summary>
    /// 单段移动完成事件：
    /// BoardEntity 每走完一段连接线后广播。
    /// </summary>
    public struct BoardEntitySegmentCompletedEvent
    {
        // 移动完成的实体
        public Gameplay.Board.Runtime.BoardEntity Entity;
        // 抵达的节点
        public MapWaypoint Node;
    }

    /// <summary>
    /// 多步移动结束事件：
    /// BoardMovementController 完成全部步数后广播。
    /// </summary>
    public struct BoardMovementEndedEvent
    {
        // 完成移动的实体
        public Gameplay.Board.Runtime.BoardEntity Entity;
    }

    /// <summary>
    /// 分叉选择请求事件：
    /// 遇到岔路时广播，外部通过回调返回选中的连接线。
    /// </summary>
    public struct BoardForkSelectionRequestedEvent
    {
        // 当前节点
        public MapWaypoint Node;
        // 可选路径列表
        public System.Collections.Generic.List<WaypointConnection> Options;
        // 选择完成回调
        public System.Action<WaypointConnection> OnSelected;
    }

    /// <summary>
    /// 掷骰子请求事件：
    /// 由棋盘菜单 UI 触发，PlayerTurnState 监听。
    /// </summary>
    public struct BoardRollDiceRequestedEvent
    {
    }

    public struct BoardActionButtonHoverEvent
    {
        public int Index;
    }

    public struct BoardActionButtonClickEvent
    {
        public int Index;
    }

    public struct BoardActionButtonExitEvent
    {
        public int Index;
    }

    /// <summary>
    /// 露营动作按钮 Hover 事件：
    /// 由 CampActionButton 在鼠标进入时触发。
    /// </summary>
    public struct CampActionButtonHoverEvent
    {
        public int Index;
    }

    /// <summary>
    /// 露营动作按钮 Click 事件：
    /// 由 CampActionButton 在点击时触发。
    /// </summary>
    public struct CampActionButtonClickEvent
    {
        public int Index;
    }

    /// <summary>
    /// 露营动作按钮 Exit 事件：
    /// 由 CampActionButton 在鼠标移出时触发。
    /// </summary>
    public struct CampActionButtonExitEvent
    {
        public int Index;
    }

    public struct InputMoveEvent
    {
        public UnityEngine.Vector2 Value;
    }

    public struct InputInteractEvent
    {
    }

    public struct InputInteractCanceledEvent
    {
    }

    public struct InputJumpEvent
    {
    }

    public struct SaveStartedEvent
    {
        // 槽位索引
        public int SlotIndex;
    }

    public struct SaveCompletedEvent
    {
        // 槽位索引
        public int SlotIndex;
        // 是否成功
        public bool Success;
        // 失败原因（成功时为空）
        public string Error;
    }

    public struct LoadStartedEvent
    {
        // 槽位索引
        public int SlotIndex;
    }

    public struct LoadCompletedEvent
    {
        // 槽位索引
        public int SlotIndex;
        // 是否成功
        public bool Success;
    }

    public struct LoadFailedEvent
    {
        // 槽位索引
        public int SlotIndex;
        // 失败原因
        public string Error;
    }

    /// <summary>
    /// 屏幕淡入/淡出请求事件：
    /// 由状态机或系统发起，UI 层监听后执行黑屏渐变。
    /// </summary>
    public struct FadeRequestedEvent
    {
        // true = 淡入到黑屏，false = 从黑屏淡出
        public bool FadeIn;
        // 渐变时长（秒）
        public float Duration;
    }

    /// <summary>
    /// 输入锁定事件：
    /// 在加载或关键过渡期间禁用输入，避免误操作。
    /// </summary>
    public struct InputLockRequestedEvent
    {
        // true = 锁定输入，false = 解锁输入
        public bool Locked;
    }
}
