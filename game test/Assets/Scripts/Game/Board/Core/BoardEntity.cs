using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.Stats;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 棋盘实体组件：负责管理棋盘上的玩家或NPC，包括移动逻辑、贝塞尔曲线路径跟随及动画更新。
    /// </summary>
    public class BoardEntity : MonoBehaviour
    {
        [Header("基础配置")]
        [SerializeField] private bool isPlayer;             // 标识当前实体是否为玩家控制
        [SerializeField] private MapWaypoint initialNode;   // 初始生成的起始地块节点
        [SerializeField] private bool snapToNodeOnStart = true; // 启动时是否立即吸附到初始节点位置

        [Header("移动与动画")]
        [SerializeField] private float moveSpeed = 5f;      // 基础移动速度
        [SerializeField] private float rotateSpeed = 15f;    // 转向目标点时的旋转插值速度
        [SerializeField] private Animator animator;         // 角色动画控制器
        [SerializeField] private string moveSpeedParamName = "Speed"; // 动画机中控制移动状态的 Float 参数名
        [SerializeField] private bool triggerConnectionEvents = false; // 移动过程中是否触发路径上的中间事件
        [SerializeField] private CharacterStats stats;      // 角色属性系统（从中读取动态移动速度）

        // 公共属性
        public bool IsPlayer => isPlayer;
        public MapWaypoint CurrentNode { get; private set; }    // 当前所处或即将到达的地块节点
        public MapWaypoint CurrentWaypoint => CurrentNode;      // CurrentNode 的别名
        public MapWaypoint LastWaypoint { get; private set; }   // 移动前停留的上一个地块节点（用于防止“折返”逻辑）
        public bool IsMoving { get; private set; }             // 当前是否正在移动中
        public bool TriggerConnectionEvents
        {
            get => triggerConnectionEvents;
            set => triggerConnectionEvents = value;
        }

        private int _animIDSpeed; // 动画参数名转化的 Hash，提高性能

        private void Awake()
        {
            _animIDSpeed = Animator.StringToHash(moveSpeedParamName);
            CacheAnimator(); // 自动查找子物体中的 Animator
            if (stats == null) stats = GetComponent<CharacterStats>();
        }

        private void OnEnable()
        {
            // 注册到管理器，统一维护实体生命周期
            if (BoardEntityManager.Instance != null)
            {
                BoardEntityManager.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            // 从管理器注销，避免脏数据残留
            if (BoardEntityManager.Instance != null)
            {
                BoardEntityManager.Instance.Unregister(this);
            }
        }

        private void Start()
        {
            // 场景开始时，如果有配置初始点则进行初始化位置
            if (CurrentNode == null && initialNode != null)
            {
                SetCurrentNode(initialNode, snapToNodeOnStart);
            }
        }

        /// <summary> 设置实体的玩家/NPC 身份 </summary>
        public void SetAsPlayer(bool value)
        {
            isPlayer = value;
        }

        /// <summary>
        /// 设置当前所在节点。
        /// </summary>
        /// <param name="node">目标节点</param>
        /// <param name="snapToNode">是否立即传送坐标到该节点</param>
        /// <param name="resetLastWaypoint">是否重置“上一个地块”记录（通常传送后需要重置）</param>
        public void SetCurrentNode(MapWaypoint node, bool snapToNode, bool resetLastWaypoint = true)
        {
            CurrentNode = node;
            if (resetLastWaypoint)
            {
                LastWaypoint = null;
            }
            if (snapToNode && node != null)
            {
                transform.position = node.transform.position;
            }
        }

        /// <summary> 设置移动状态标识 </summary>
        public void SetMovingState(bool value)
        {
            IsMoving = value;
        }

        /// <summary> 更新动画控制器的速度参数 </summary>
        public void SetMoveAnimationSpeed(float value)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(_animIDSpeed, value);
        }

        /// <summary>
        /// 启动移动逻辑（不考虑分叉交互的简化版本）
        /// </summary>
        /// <param name="steps">移动步数</param>
        public void MoveTo(int steps)
        {
            if (IsMoving) return;
            if (CurrentNode == null)
            {
                Debug.LogWarning("[BoardEntity] CurrentNode 为空，无法开始移动。");
                return;
            }
            StartCoroutine(MoveRoutine(steps));
        }

        /// <summary>
        /// 移动逻辑的主协程
        /// </summary>
        private IEnumerator MoveRoutine(int totalSteps)
        {
            SetMovingState(true);
            SetMoveAnimationSpeed(1f); // 开启跑步/行走动画

            int stepsRemaining = totalSteps;
            while (stepsRemaining > 0)
            {
                if (CurrentNode == null || CurrentNode.connections.Count == 0) break;

                // 寻找下一个连接节点（处理路径选择）
                WaypointConnection connection = ChooseNextConnection(CurrentNode);
                if (connection == null) break;

                // 沿着路径线段移动（直到到达该段终点）
                yield return StartCoroutine(MoveAlongConnection(connection));
                stepsRemaining--;
            }

            SetMoveAnimationSpeed(0f); // 恢复待机动画
            SetMovingState(false);
            EventBus.Raise(new BoardEntityMoveEndedEvent { Entity = this }); // 广播全局事件，便于解耦监听
        }

        /// <summary>
        /// 根据当前节点决定下一步走哪条路径。
        /// 如果有分叉，目前会随机选择（不包含UI交互逻辑）。
        /// </summary>
        private WaypointConnection ChooseNextConnection(MapWaypoint node)
        {
            if (node == null || node.connections.Count == 0) return null;

            // 获取合法地块（通常会排除掉掉头回来的地块）
            List<MapWaypoint> validTargets = node.GetValidNextNodes(LastWaypoint);
            if (validTargets.Count == 0) return null;

            // 单一路径直接走
            if (validTargets.Count == 1) return node.GetConnectionTo(validTargets[0]);

            // 多路径随机选（如果需要玩家选路，逻辑通常在此处拦截）
            int index = UnityEngine.Random.Range(0, validTargets.Count);
            return node.GetConnectionTo(validTargets[index]);
        }

        /// <summary>
        /// 核心移动协程：沿着两个节点间的二阶贝塞尔曲线平滑移动。
        /// </summary>
        public IEnumerator MoveAlongConnection(WaypointConnection conn)
        {
            if (conn == null || conn.targetNode == null || CurrentNode == null) yield break;

            Vector3 p0 = transform.position; // 起点
            Vector3 p2 = conn.targetNode.transform.position; // 终点
            Vector3 curveStartPos = CurrentNode.transform.position;
            Vector3 p1 = curveStartPos + conn.controlPointOffset; // 贝塞尔控制点

            // 粗略估算路径长度以计算时间
            float approxDist = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
            float speed = stats != null ? stats.MoveSpeed.Value : moveSpeed;

            if (speed <= 0f || approxDist <= 0f)
            {
                transform.position = p2;
                SetCurrentNode(conn.targetNode, false);
                yield break;
            }

            float duration = approxDist / speed;
            int nextEventIndex = 0;
            int totalEvents = conn.events.Count;
            float timer = 0f;

            while (timer < duration)
            {
                float dt = Time.deltaTime;
                float nextTimer = timer + dt;
                float nextT = nextTimer / duration; // 归一化进度 (0-1)

                // 检查路径上是否有待触发的“中间事件”（例如路径上的陷阱、采集点）
                if (triggerConnectionEvents && nextEventIndex < totalEvents && conn.events[nextEventIndex].progressPoint <= nextT)
                {
                    ConnectionEvent evt = conn.events[nextEventIndex];
                    float triggerT = evt.progressPoint;

                    // 将实体精准对齐到事件触发点位置
                    Vector3 triggerPos = BezierUtils.GetQuadraticBezierPoint(triggerT, curveStartPos, p1, p2);
                    transform.position = triggerPos;
                    timer = triggerT * duration;

                    // 挂起移动逻辑，等待事件执行完毕
                    yield return StartCoroutine(HandleConnectionEvent(evt));

                    nextEventIndex++;
                    continue; // 继续剩余的移动
                }

                timer = nextTimer;
                // 计算贝塞尔曲线上的下一个坐标点
                Vector3 nextPos = BezierUtils.GetQuadraticBezierPoint(nextT, curveStartPos, p1, p2);

                // 处理旋转：让角色看向移动的方向
                Vector3 moveDir = (nextPos - transform.position).normalized;
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
                }

                transform.position = nextPos;
                yield return null;
            }

            // 到达本段终点，更新节点信息
            transform.position = p2;
            LastWaypoint = CurrentNode;
            SetCurrentNode(conn.targetNode, false, false);
        }

        /// <summary>
        /// 处理路径中途触发的特定事件（如过场动画或即时奖励）。
        /// </summary>
        private IEnumerator HandleConnectionEvent(ConnectionEvent evt)
        {
            SetMoveAnimationSpeed(0f); // 停下动画

            if (evt.eventAction != null)
            {
                // 执行 ScriptableObject 类型的事件动作
                yield return StartCoroutine(evt.eventAction.Execute(BoardGameManager.Instance, evt.contextTarget));
            }
            else
            {
                Debug.LogWarning("触发了路径事件，但未分配 EventAction 脚本对象！");
            }

            SetMoveAnimationSpeed(1f); // 恢复移动动画
        }

        /// <summary>
        /// 缓存 Animator。如果当前对象没有，则在子物体中深度搜索。
        /// </summary>
        private void CacheAnimator()
        {
            if (animator != null) return;
            Animator[] animators = GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i].runtimeAnimatorController == null) continue;
                animator = animators[i];
                return;
            }
        }
    }
}
