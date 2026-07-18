using UnityEngine;
using UnityEngine.AI;

namespace IndieGame.Gameplay.Combat
{
    /// <summary>
    /// 战斗单位移动组件（NavMeshAgent 包装）：
    /// 负责追击目标（节流重寻路）、停止与面向目标。
    /// 位移完全由 NavMeshAgent 驱动——战斗体上不允许挂 CharacterController，避免双写 transform 互踩。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class CombatUnitMover : MonoBehaviour
    {
        [Tooltip("面向目标的转身速度（度/秒）")]
        [SerializeField] private float turnSpeed = 540f;

        private NavMeshAgent _agent;
        // 重寻路节流：避免逐帧 SetDestination 触发路径重算
        private float _repathInterval = 0.2f;
        private float _nextRepathTime;
        // 当前追击目标（用于判断目标切换时立即重寻路）
        private Transform _chaseTarget;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// 初始化移动参数（由 CombatUnit.Initialize 调用）。
        /// </summary>
        public void Initialize(float moveSpeed, float repathInterval)
        {
            _repathInterval = Mathf.Max(0.05f, repathInterval);
            _nextRepathTime = 0f;
            _chaseTarget = null;
            if (_agent != null)
            {
                _agent.speed = Mathf.Max(0.1f, moveSpeed);
                // 攻击时由本组件手动转身（保证攻击朝向），移动中让 Agent 自行转向
                _agent.updateRotation = true;
            }
        }

        /// <summary>
        /// 刷新移动速度（属性加成变化后调用）。
        /// </summary>
        public void SetMoveSpeed(float moveSpeed)
        {
            if (_agent != null) _agent.speed = Mathf.Max(0.1f, moveSpeed);
        }

        /// <summary>
        /// 追击目标：按节流间隔重设寻路终点；目标切换时立即重寻路。
        /// </summary>
        /// <param name="target">追击目标</param>
        /// <param name="stopRange">期望的停止距离（通常为攻击射程的九成，留出出手余量）</param>
        public void ChaseTarget(Transform target, float stopRange)
        {
            if (_agent == null || target == null || !_agent.isOnNavMesh) return;

            _agent.stoppingDistance = Mathf.Max(0f, stopRange);
            _agent.isStopped = false;

            bool targetChanged = _chaseTarget != target;
            if (!targetChanged && Time.time < _nextRepathTime) return;

            _chaseTarget = target;
            _nextRepathTime = Time.time + _repathInterval;
            _agent.SetDestination(target.position);
        }

        /// <summary>
        /// 停止移动并清空路径（进入攻击距离/失去目标/单位停摆时调用）。
        /// </summary>
        public void Halt()
        {
            _chaseTarget = null;
            if (_agent == null || !_agent.isOnNavMesh) return;
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        /// <summary>
        /// 平滑面向目标（攻击时保持朝向，仅旋转 Y 轴）。
        /// </summary>
        public void FaceTarget(Transform target)
        {
            if (target == null) return;
            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, turnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 瞬移到指定位置（上场放置用，走 Agent.Warp 保证落在 NavMesh 上）。
        /// </summary>
        /// <returns>true = 落点有效并完成瞬移</returns>
        public bool WarpTo(Vector3 position)
        {
            if (_agent == null) return false;
            return _agent.Warp(position);
        }
    }
}
