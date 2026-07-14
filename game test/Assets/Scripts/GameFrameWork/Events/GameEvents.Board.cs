using System;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Gameplay.Board.Runtime;
using IndieGame.Gameplay.Inventory;
using IndieGame.Gameplay.Dialogue;

namespace IndieGame.Core
{
    // 棋盘玩法事件：节点抵达、实体移动、分叉选择、格子效果、操作菜单、宝具菜单
    // （L5 重构：原 GameEvents.cs 单文件 1000+ 行，按领域拆分为 GameEvents.*.cs 多文件，
    // 命名空间与全部类型定义保持不变，纯文件级重组。）
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
    /// 格子请求额外移动事件：
    /// 前进格/后退格触发后广播，BoardMovementController 监听并追加步数。
    /// Steps 为正 = 前进，为负 = 后退。
    /// </summary>
    public struct BoardExtraMoveRequestedEvent
    {
        public int Steps;
    }

    /// <summary>
    /// 方向格移动请求事件：
    /// DirectionalMoveTile 触发后广播，BoardMovementController 在最终落点时将步数重置为 Steps，
    /// 并锁定首步方向为 DirectionNodeId 指定的节点（跳过分叉UI）；路过时自动丢弃。
    /// </summary>
    public struct BoardDirectionalMoveRequestedEvent
    {
        /// <summary> 移动总格数（大于0） </summary>
        public int Steps;
        /// <summary> 首步强制走向的节点 nodeID，决定行进方向 </summary>
        public int DirectionNodeId;
    }

    /// <summary>
    /// 扭曲格强制滑行请求事件：
    /// WarpTile 触发后广播，BoardMovementController 在最终落点时追加1步并锁定方向（跳过分叉UI）；
    /// 路过时自动丢弃。
    /// </summary>
    public struct BoardWarpSlideRequestedEvent
    {
        /// <summary> 强制滑行目标节点的 nodeID（当前路口的直接出口之一） </summary>
        public int ForcedNodeId;
    }

    /// <summary>
    /// 扭曲格路径过滤请求事件：
    /// WarpTile 触发后广播，BoardMovementController 在下次分叉选择时从候选出口中移除该节点，
    /// 实现保护指定路径不被玩家选择的效果。
    /// </summary>
    public struct BoardWarpFilterPathEvent
    {
        /// <summary> 需要从分叉UI中隐藏的路径入口节点 nodeID </summary>
        public int ProtectedNodeId;
    }

    /// <summary>
    /// 掷骰子请求事件：
    /// 由棋盘菜单 UI 触发，PlayerTurnState 监听。
    /// </summary>
    public struct BoardRollDiceRequestedEvent
    {
    }

    /// <summary>
    /// 操作菜单显示事件：
    /// BoardActionMenuView.Show() 成功显示菜单后广播，供镜头/角色朝向系统监听。
    /// </summary>
    public struct BoardActionMenuShownEvent
    {
        // 菜单围绕的目标（通常是玩家 Transform）
        public Transform Target;
    }

    /// <summary>
    /// 操作菜单镜头拉远完成事件：
    /// ActionMenuCameraController 检测到投骰子后的镜头 Blend 已经结束（切回主镜头）时广播。
    /// PlayerTurnState 监听该事件后才真正切换到 MovementState，避免角色在镜头还没拉远完就开始走。
    /// </summary>
    public struct BoardActionMenuCameraSettledEvent
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
    /// 宝具菜单打开请求事件：
    /// 由 BoardActionMenuView 在玩家点击"宝具"按钮时发布，TreasureMenuView 订阅后自行展示。
    /// </summary>
    public struct BoardTreasureMenuRequestedEvent { }

    /// <summary>
    /// 宝具选中事件：
    /// TreasureMenuView 在玩家确认选择某个宝具时发布，PlayerTurnState 监听后切换到对应激活状态。
    /// </summary>
    public struct TreasureItemSelectedEvent
    {
        // 选中宝具的唯一 ID（与 TreasureSO.TreasureId 对应）
        public string TreasureId;
    }

    /// <summary>
    /// 宝具菜单取消事件：
    /// TreasureMenuView 在玩家按取消键时发布，PlayerTurnState 监听后重新显示操作菜单。
    /// </summary>
    public struct TreasureMenuCancelledEvent { }
    /// <summary>
    /// 人体大炮弹射请求事件：
    /// CannonTile 触发后广播，BoardMovementController 消费后随机选目标并执行抛物线弹射。
    /// </summary>
    public struct BoardCannonLaunchRequestedEvent
    {
        // 抛物线峰值高度
        public float ArcHeight;
        // 弹射速度
        public float LaunchSpeed;
        // 飞行时Y轴自转速度（度/秒），0=不转体（保持原来面向目标的旋转）
        public float SpinSpeed;
        // 落地减速阶段在对齐目标朝向前额外旋转的圈数（增强减速感）
        public float SettleExtraRotations;
    }

    /// <summary>
    /// 传送格传送请求事件：
    /// TeleportTile 触发后广播，BoardMovementController 消费后瞬移玩家并触发目标格子效果。
    /// </summary>
    public struct BoardTeleportRequestedEvent
    {
        // 目标节点 ID（由 TeleportTile Inspector 配置）
        public int TargetNodeId;
    }

    /// <summary>
    /// 连锁移位倍率变更事件：
    /// ComboMoveSystem 在 Combo 计数发生变化时广播（每次位移格触发时 +1，每次掷骰开始时归零）。
    /// UI 层订阅此事件显示连击提示；奖励系统可读取 Multiplier 字段直接使用。
    /// </summary>
    public struct ComboMoveUpdatedEvent
    {
        // 当前连锁触发次数（0 表示已归零）
        public int ComboCount;
        // 当前奖励倍率（= 1 + ComboCount）
        public float Multiplier;
    }
}
