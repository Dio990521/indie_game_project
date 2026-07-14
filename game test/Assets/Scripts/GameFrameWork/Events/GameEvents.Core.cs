using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Dialogue;

namespace IndieGame.Core
{
    // 核心流程事件：游戏状态、场景过渡、存档/读档、自动存档、输入、黑屏与输入锁
    // （L5 重构：原 GameEvents.cs 单文件 1000+ 行，按领域拆分为 GameEvents.*.cs 多文件，
    // 命名空间与全部类型定义保持不变，纯文件级重组。）
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
    // ===================== 存档系统事件 =====================

    /// <summary>
    /// 自动存档触发来源：
    /// 该枚举用于描述”自动存档请求是由哪个业务场景发起”的。
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
        Sleep = 1,
        // 城镇旅馆住宿触发的自动存档。
        Inn = 2,
        // 打造成功触发的自动存档。
        Craft = 3
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
