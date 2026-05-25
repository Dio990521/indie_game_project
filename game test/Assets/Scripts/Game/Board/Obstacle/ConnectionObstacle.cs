using DG.Tweening;
using IndieGame.Core;
using IndieGame.Core.Utilities;
using UnityEngine;

namespace IndieGame.Gameplay.Board.Runtime
{
    /// <summary>
    /// 连接线障碍物视觉组件（纯表现层，不参与寻路逻辑）：
    /// 挂载在场景中的障碍物 GameObject（如村庄铁门）上，根据 GameFlagSystem 中对应 Flag
    /// 的状态自动切换开/关状态并播放 DoTween 动画。
    ///
    /// 工作流：
    /// 1. 在 Inspector 中填入 flagKey（与对应 WaypointConnection.obstacleKey 相同）；
    /// 2. 拖入 obstacleVisual（门板/闸门模型的根节点）；
    /// 3. 在 Scene 视图中摆好"关闭位置"后，记录 closedLocalPos；再摆好"开启位置"后记录 openLocalPos；
    /// 4. 当 GameFlagSystem.SetFlag(flagKey, true) 被调用时，自动播放开门动画。
    ///
    /// 注意：此组件不直接控制路径通断，路径通断由 WaypointConnection.IsBlocked() 决定。
    /// </summary>
    public class ConnectionObstacle : MonoBehaviour
    {
        [Header("Flag 关联")]
        [Tooltip("对应 WaypointConnection.obstacleKey 中填写的相同 Key。\n" +
                 "Flag=false → 障碍物关闭（封路）；Flag=true → 障碍物开启（通行）。")]
        [SerializeField] private string flagKey;

        [Header("视觉设置")]
        [Tooltip("障碍物的可动部分（如门板），动画将移动此物体的本地坐标。")]
        [SerializeField] private Transform obstacleVisual;

        [Tooltip("障碍物开启（可通行）时 obstacleVisual 的本地坐标。")]
        [SerializeField] private Vector3 openLocalPos;

        [Tooltip("障碍物关闭（封路）时 obstacleVisual 的本地坐标。")]
        [SerializeField] private Vector3 closedLocalPos;

        [Tooltip("开关动画时长（秒）。")]
        [SerializeField] private float animDuration = 0.5f;

        [Tooltip("开关动画缓动类型。")]
        [SerializeField] private Ease animEase = Ease.OutQuad;

        // ── 生命周期 ──────────────────────────────────────────────────────────

        private void Start()
        {
            if (obstacleVisual == null)
            {
                DebugTools.LogWarning($"[ConnectionObstacle] obstacleVisual 未赋值，请检查 {gameObject.name}。");
                return;
            }

            // 初始化：直接同步位置，不播放动画
            bool isOpen = GameFlagSystem.Instance != null && GameFlagSystem.Instance.GetFlag(flagKey);
            obstacleVisual.localPosition = isOpen ? openLocalPos : closedLocalPos;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameFlagChangedEvent>(OnFlagChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameFlagChangedEvent>(OnFlagChanged);
        }

        // ── 事件处理 ──────────────────────────────────────────────────────────

        private void OnFlagChanged(GameFlagChangedEvent evt)
        {
            if (evt.Key != flagKey) return;

            if (evt.NewValue)
                PlayOpen();
            else
                PlayClose();
        }

        // ── 动画 ──────────────────────────────────────────────────────────────

        private void PlayOpen()
        {
            if (obstacleVisual == null) return;
            obstacleVisual.DOKill();
            obstacleVisual.DOLocalMove(openLocalPos, animDuration).SetEase(animEase);
        }

        private void PlayClose()
        {
            if (obstacleVisual == null) return;
            obstacleVisual.DOKill();
            obstacleVisual.DOLocalMove(closedLocalPos, animDuration).SetEase(animEase);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Scene 辅助绘制：在编辑器中显示开/关目标位置，方便调整。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (obstacleVisual == null) return;

            Matrix4x4 parentMatrix = obstacleVisual.parent != null
                ? obstacleVisual.parent.localToWorldMatrix
                : Matrix4x4.identity;

            Vector3 worldOpen   = parentMatrix.MultiplyPoint3x4(openLocalPos);
            Vector3 worldClosed = parentMatrix.MultiplyPoint3x4(closedLocalPos);

            // 绿色 = 开启目标位置
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(worldOpen, Vector3.one * 0.3f);

            // 红色 = 关闭目标位置
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(worldClosed, Vector3.one * 0.3f);

            // 连线
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(worldOpen, worldClosed);
        }
#endif
    }
}
