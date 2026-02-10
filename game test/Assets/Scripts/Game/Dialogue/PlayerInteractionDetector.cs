using IndieGame.Core;
using UnityEngine;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 玩家交互目标检测器：
    /// 该组件的唯一职责是“在玩家附近找出当前最合适的可交互对象”。
    ///
    /// 设计原则（与交互控制器解耦）：
    /// 1) Detector 只做目标检测与目标切换通知，不处理输入按键。
    /// 2) Controller 只在玩家按下 Interact 时读取 Detector 的当前目标并执行交互。
    /// 3) UI 可订阅 EventBus 的目标变更事件来显示/隐藏“按键提示”。
    /// </summary>
    public class PlayerInteractionDetector : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("检测原点（一般拖玩家胸口或头部锚点；不配则使用自身 Transform）")]
        [SerializeField] private Transform detectionOrigin;

        [Tooltip("交互检测半径（单位：米）")]
        [SerializeField] private float detectionRadius = 2.4f;

        [Tooltip("周期扫描间隔（秒）。值越小越实时，值越大越省性能。")]
        [SerializeField] private float scanInterval = 0.08f;

        [Tooltip("交互对象所在层。建议单独建 Interactable 层并只勾选它。")]
        [SerializeField] private LayerMask interactableMask = ~0;

        [Header("Scoring")]
        [Tooltip("最小朝向点积。0=只允许玩家前半球，-1=全方向。推荐 0~0.2。")]
        [Range(-1f, 1f)]
        [SerializeField] private float minForwardDot = 0.05f;

        [Tooltip("是否强制要求可见性（可用于防止隔墙交互）。")]
        [SerializeField] private bool requireLineOfSight = false;

        [Tooltip("可见性检测层（通常是地形+障碍物层，不建议包含 Interactable 层）。")]
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Debug")]
        [Tooltip("是否在 Scene 视图绘制检测 Gizmo。")]
        [SerializeField] private bool drawGizmo = true;

        // 物理查询缓存：使用 NonAlloc API，避免每次扫描都产生 GC。
        private readonly Collider[] _overlapBuffer = new Collider[32];

        // 当前锁定目标（Controller 会读取这个目标来执行交互）
        private IInteractable _currentTarget;
        private GameObject _currentTargetObject;

        // 下一次允许扫描的时间（节流）
        private float _nextScanTime;

        /// <summary>
        /// 当前是否存在可交互目标。
        /// </summary>
        public bool HasTarget => _currentTarget != null && _currentTargetObject != null;

        /// <summary>
        /// 获取当前目标：
        /// 返回 false 代表当前没有可交互目标。
        /// </summary>
        public bool TryGetCurrentTarget(out IInteractable target, out GameObject targetObject)
        {
            target = _currentTarget;
            targetObject = _currentTargetObject;
            return target != null && targetObject != null;
        }

        private void Update()
        {
            // 只在自由探索状态工作：
            // 这样不会和棋盘流程、菜单流程产生目标误检开销。
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.FreeRoam)
            {
                SetCurrentTarget(null, null);
                return;
            }

            if (Time.unscaledTime < _nextScanTime) return;
            _nextScanTime = Time.unscaledTime + Mathf.Max(0.01f, scanInterval);

            ScanBestTarget();
        }

        /// <summary>
        /// 扫描附近可交互对象，并按“朝向 + 距离”打分选出最优目标。
        /// </summary>
        private void ScanBestTarget()
        {
            Vector3 origin = GetOriginPosition();
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                Mathf.Max(0.01f, detectionRadius),
                _overlapBuffer,
                interactableMask,
                QueryTriggerInteraction.Collide);

            IInteractable bestTarget = null;
            GameObject bestObject = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit == null) continue;

                if (!TryResolveInteractable(hit, out IInteractable interactable, out GameObject owner)) continue;
                if (interactable == null || owner == null) continue;
                if (owner == gameObject) continue;

                Vector3 toTarget = owner.transform.position - origin;
                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance <= 0.0001f) continue;

                // 朝向过滤：
                // 只有在玩家前方（点积大于阈值）的目标才可进入候选。
                float dot = Vector3.Dot(transform.forward, toTarget.normalized);
                if (dot < minForwardDot) continue;

                if (requireLineOfSight && !HasLineOfSight(origin, owner)) continue;

                // 打分策略：
                // - 朝向越正（dot 越接近 1）优先级越高
                // - 距离越近优先级越高
                // 系数上把朝向权重略提高，减少“侧后方目标误抢焦点”的体感问题。
                float distanceScore = 1f / (1f + sqrDistance);
                float score = dot * 2f + distanceScore;

                if (score <= bestScore) continue;
                bestScore = score;
                bestTarget = interactable;
                bestObject = owner;
            }

            SetCurrentTarget(bestTarget, bestObject);
        }

        /// <summary>
        /// 从碰撞体解析出 IInteractable：
        /// 允许交互脚本挂在父物体上，因此向父级链搜索。
        /// </summary>
        private static bool TryResolveInteractable(Collider hit, out IInteractable interactable, out GameObject owner)
        {
            interactable = null;
            owner = null;
            if (hit == null) return false;

            // Unity 对“接口型 GetComponent”在不同版本/场景下可用性不完全一致。
            // 为了稳定，这里先取 MonoBehaviour，再用 is IInteractable 过滤。
            MonoBehaviour[] behaviours = hit.GetComponentsInParent<MonoBehaviour>(includeInactive: true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;
                if (!(behaviour is IInteractable candidate)) continue;

                interactable = candidate;
                owner = behaviour.gameObject;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 线性可见性检测：
        /// 若中间被障碍物阻挡，则视为不可交互。
        /// </summary>
        private bool HasLineOfSight(Vector3 origin, GameObject target)
        {
            if (target == null) return false;

            Vector3 targetPos = target.transform.position + Vector3.up * 0.8f;
            Vector3 startPos = origin + Vector3.up * 0.8f;
            Vector3 dir = targetPos - startPos;
            float dist = dir.magnitude;
            if (dist <= 0.01f) return true;

            if (!Physics.Raycast(startPos, dir.normalized, out RaycastHit hit, dist, lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // 如果射线第一个命中就是目标（或目标子物体），说明视线畅通。
            return hit.collider != null && hit.collider.transform.IsChildOf(target.transform);
        }

        /// <summary>
        /// 更新当前目标并广播“目标变更事件”。
        /// </summary>
        private void SetCurrentTarget(IInteractable target, GameObject targetObject)
        {
            bool changed = !ReferenceEquals(_currentTarget, target) || _currentTargetObject != targetObject;
            if (!changed) return;

            _currentTarget = target;
            _currentTargetObject = targetObject;

            EventBus.Raise(new PlayerInteractableTargetChangedEvent
            {
                HasTarget = _currentTarget != null && _currentTargetObject != null,
                Target = _currentTargetObject
            });
        }

        /// <summary>
        /// 获取检测起点位置。
        /// </summary>
        private Vector3 GetOriginPosition()
        {
            return detectionOrigin != null ? detectionOrigin.position : transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Gizmos.color = HasTarget ? new Color(1f, 0.84f, 0f, 0.8f) : new Color(0.2f, 0.9f, 1f, 0.7f);
            Gizmos.DrawWireSphere(GetOriginPosition(), Mathf.Max(0.01f, detectionRadius));
        }
    }
}
