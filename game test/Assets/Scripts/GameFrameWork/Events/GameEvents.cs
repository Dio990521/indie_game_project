using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Dialogue;

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
    /// 对话界面显示请求事件：
    /// 由 DialogueManager 发布，DialogueUIView 监听并执行显示动画。
    /// </summary>
    public struct DialogueShowRequestEvent
    {
        // 是否播放显示动画
        public bool Animate;
    }

    /// <summary>
    /// 对话界面隐藏请求事件：
    /// 由 DialogueManager 发布，DialogueUIView 监听并执行隐藏动画。
    /// </summary>
    public struct DialogueHideRequestEvent
    {
        // 是否播放隐藏动画
        public bool Animate;
    }

    /// <summary>
    /// 对话说话人文本更新事件：
    /// Manager 在切换到新句子后发布，View 只负责渲染文本。
    /// </summary>
    public struct DialogueSpeakerChangedEvent
    {
        // 当前句子的说话人（本地化解析后的最终字符串）
        public string SpeakerText;
    }

    /// <summary>
    /// 对话打字机启动请求事件：
    /// Manager 传入已经完成富文本处理的文本，View 按速度执行逐字显示。
    /// </summary>
    public struct DialogueTypewriterRequestEvent
    {
        // 富文本格式内容（已包含关键词高亮）
        public string FormattedText;
        // 每个可见字符之间的间隔（秒）
        public float Speed;
    }

    /// <summary>
    /// 对话打字机跳过请求事件：
    /// 当玩家在打字中按下交互键时，Manager 发布该事件，View 应立即展示完整文本。
    /// </summary>
    public struct DialogueSkipTypewriterRequestEvent
    {
    }

    /// <summary>
    /// 对话打字机完成事件：
    /// View 在“自然打完”或“被跳过后立即显示完整文本”两种情况下都要发布此事件，
    /// Manager 监听后把 IsTyping 切换为 false。
    /// </summary>
    public struct DialogueTypewriterCompletedEvent
    {
    }

    /// <summary>
    /// 对话开始事件：
    /// 方便日志系统、教程系统或其他模块监听“进入对话”的时机。
    /// </summary>
    public struct DialogueStartedEvent
    {
        // 当前启动的对话资源
        public DialogueDataSO DialogueData;
    }

    /// <summary>
    /// 对话结束事件：
    /// 用于外部系统监听“对话流程已结束”。
    /// </summary>
    public struct DialogueEndedEvent
    {
        // 刚刚结束的对话资源
        public DialogueDataSO DialogueData;
    }

    /// <summary>
    /// 词条习得事件：
    /// 当对话行中的关键词成功命中并被学习后发布。
    /// </summary>
    public struct DialogueWordLearnedEvent
    {
        // 词条唯一 ID
        public string WordID;
        // 词条资源引用（可选，供 UI 或日志读取更多信息）
        public WordSO Word;
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

    /// <summary>
    /// 自动存档触发来源：
    /// 该枚举用于描述“自动存档请求是由哪个业务场景发起”的。
    /// 设计价值：
    /// 1) 让 AutoSaveService 可以按来源应用不同槽位策略；
    /// 2) 让日志与问题定位更清晰（例如 Sleep 自动存档失败）；
    /// 3) 为后续扩展（战斗结束、场景切换、Boss 击败）预留稳定枚举位。
    /// </summary>
    public enum AutoSaveReason
    {
        // 未指定来源（兜底值，尽量不要在业务层主动使用）。
        None = 0,
        // 露营睡觉触发的自动存档。
        Sleep = 1
    }

    /// <summary>
    /// 自动存档请求事件：
    /// 任何业务模块（当前为 CampUIView）都通过该事件向 AutoSaveService 提交保存请求。
    /// 注意：
    /// - 这是“全局统一入口”，后续其他系统接入时复用该事件即可；
    /// - 本事件本身不执行保存，只负责把请求参数传给服务层。
    /// </summary>
    public struct AutoSaveRequestedEvent
    {
        // 请求唯一 ID（由请求方生成）：用于在完成回调里做精确匹配，避免串线。
        public int RequestId;
        // 自动存档触发来源（用于槽位路由、统计、日志）。
        public AutoSaveReason Reason;
        // 目标槽位：>=0 表示请求方强制指定；<0 表示让 AutoSaveService 按策略决定。
        public int SlotIndex;
        // 存档备注（可选，空时由 AutoSaveService 按 Reason 自动生成）。
        public string Note;
        // 是否需要业务层等待完成事件后再继续流程（例如 Sleep 黑屏流程通常需要等待）。
        public bool WaitForCompletion;
    }

    /// <summary>
    /// 自动存档完成事件：
    /// 由 AutoSaveService 在单次请求处理结束后发布，发起方可根据 RequestId 匹配自己的请求。
    /// </summary>
    public struct AutoSaveCompletedEvent
    {
        // 对应请求 ID（与 AutoSaveRequestedEvent.RequestId 对齐）。
        public int RequestId;
        // 自动存档触发来源（原样回传请求值，便于接收方二次校验）。
        public AutoSaveReason Reason;
        // 实际执行的槽位。
        public int SlotIndex;
        // 是否成功。
        public bool Success;
        // 失败原因（成功时可为空）。
        public string Error;
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

    /// <summary>
    /// 玩家“当前可交互目标”变更事件：
    /// 由 PlayerInteractionDetector 在目标切换时广播。
    /// 常见用途：交互提示 UI（如“按 E 对话”）的显示/隐藏与目标名刷新。
    /// </summary>
    public struct PlayerInteractableTargetChangedEvent
    {
        // 是否存在可交互目标
        public bool HasTarget;
        // 当前目标对象（HasTarget=false 时为 null）
        public GameObject Target;
    }

    /// <summary>
    /// 玩家交互执行完成事件：
    /// 由 PlayerInteractionController 在成功调用 IInteractable.Interact 后广播。
    /// 常见用途：音效、日志、教学引导统计。
    /// </summary>
    public struct PlayerInteractionPerformedEvent
    {
        // 发起交互的对象（通常是玩家）
        public GameObject Interactor;
        // 被交互的目标对象
        public GameObject Target;
    }

    public struct SaveStartedEvent
    {
        // 槽位索引
        public int SlotIndex;
        // 保存来源标记（可选）：
        // 例如 "AutoSaveService:Sleep:12"、"ManualSave:UIButton"。
        // 用于让上层系统在监听 SaveCompletedEvent 时进行精准匹配与调试定位。
        public string SourceTag;
    }

    /// <summary>
    /// 打开标题读档菜单事件：
    /// 由标题界面入口（Load 按钮）发起，SaveLoadMenuView 监听后展示列表。
    /// </summary>
    public struct OpenSaveLoadMenuEvent
    {
    }

    /// <summary>
    /// 关闭标题读档菜单事件：
    /// 用于统一关闭逻辑（如点击关闭按钮、读档成功后自动关闭）。
    /// </summary>
    public struct CloseSaveLoadMenuEvent
    {
    }

    /// <summary>
    /// 标题界面“已确认读取存档”事件：
    /// 由 SaveLoadMenuView 在玩家点击确认后、真正发起 LoadAsync 前发布。
    /// 设计目的：
    /// 1) 让 TitleScreenManager 明确知道“本次 LoadCompletedEvent 来自标题读档流程”；
    /// 2) 避免将来其他系统在标题场景触发 Load 时被误判为“应自动进游戏”；
    /// 3) 保持菜单视图与标题主流程解耦（通过 EventBus 传递意图）。
    /// </summary>
    public struct TitleLoadGameRequestedEvent
    {
        // 玩家确认读取的槽位索引（用于日志或后续扩展 UI 提示）。
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
        // 保存来源标记（与 SaveStartedEvent.SourceTag 对应）。
        public string SourceTag;
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
