using System;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;

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
}
