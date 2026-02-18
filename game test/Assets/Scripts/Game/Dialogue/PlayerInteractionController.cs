using IndieGame.Core;
using IndieGame.Core.Utilities;
using DG.Tweening;
using UnityEngine;

namespace IndieGame.Gameplay.Dialogue
{
    /// <summary>
    /// 玩家交互控制器：
    /// 负责“把 Interact 输入转换为一次具体交互调用”。
    ///
    /// 工作流程：
    /// 1) 监听 GameInputReader 广播的 InputInteractEvent。
    /// 2) 判断当前状态是否允许发起新交互（例如对话进行中则不重复触发 NPC）。
    /// 3) 从 PlayerInteractionDetector 读取当前候选目标并调用 IInteractable.Interact。
    /// </summary>
    [RequireComponent(typeof(PlayerInteractionDetector))]
    public class PlayerInteractionController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("附近目标检测器（不配时会自动从同物体查找）")]
        [SerializeField] private PlayerInteractionDetector detector;

        [Header("Rules")]
        [Tooltip("仅在 FreeRoam 状态允许发起交互。建议保持开启，避免和棋盘流程冲突。")]
        [SerializeField] private bool onlyAllowInFreeRoam = true;

        [Tooltip("交互触发冷却（秒），用于防止按键抖动导致重复触发。")]
        [SerializeField] private float interactCooldown = 0.15f;
        [Tooltip("当交互目标是 NPC 时，交互前先平滑转向 NPC。")]
        [SerializeField] private bool faceTargetBeforeInteract = true;

        [Header("Facing Tween (Reusable)")]
        [Tooltip("转向时长（秒）")]
        [SerializeField] private float facingDuration = 0.2f;
        [Tooltip("转向缓动曲线")]
        [SerializeField] private Ease facingEase = Ease.OutSine;
        [Tooltip("是否只绕 Y 轴平面转向")]
        [SerializeField] private bool facingYAxisOnly = true;
        [Tooltip("角度差小于该阈值时直接转到位，不播放 Tween")]
        [SerializeField] private float facingInstantThresholdAngle = 1f;

        // 下一次允许触发交互的时间
        private float _nextAllowedInteractTime;
        // 是否正处于“转身后再交互”的等待阶段（用于防重入）
        private bool _isWaitingFacingComplete;
        // 当前正在执行的转向 Tween（由调用方自己管理生命周期）
        private Tween _activeFacingTween;

        private void Awake()
        {
            if (detector == null)
            {
                detector = GetComponent<PlayerInteractionDetector>();
            }

        }

        private void OnEnable()
        {
            EventBus.Subscribe<InputInteractEvent>(HandleInteractInput);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InputInteractEvent>(HandleInteractInput);
            _isWaitingFacingComplete = false;
            SmoothFacingTween.Kill(ref _activeFacingTween);
        }

        /// <summary>
        /// 处理玩家交互输入：
        /// 这里不会推动对话文本（那部分由 DialogueManager 自己处理），
        /// 这里只负责“发起新的世界交互”。
        /// </summary>
        private void HandleInteractInput(InputInteractEvent evt)
        {
            // 去抖冷却：短时间内忽略重复按下。
            if (Time.unscaledTime < _nextAllowedInteractTime) return;
            if (_isWaitingFacingComplete) return;

            // 若当前已经在对话中，说明这个输入应该交给 DialogueManager 处理“跳过/下一句”，
            // 不应再次尝试触发 NPC.Interact（否则会造成重复开对话）。
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) return;

            if (onlyAllowInFreeRoam && GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.FreeRoam)
            {
                return;
            }

            if (detector == null) return;
            if (!detector.TryGetCurrentTarget(out IInteractable target, out GameObject targetObject)) return;
            if (target == null || targetObject == null) return;

            // 若本次目标是 NPC，且开启了“交互前平滑转向”，
            // 则先做 DOTween 转向，结束后再真正调用 Interact。
            if (faceTargetBeforeInteract && TryGetNpcRootTransform(targetObject, out Transform npcRoot))
            {
                _isWaitingFacingComplete = true;
                SmoothFacingTweenOptions options = new SmoothFacingTweenOptions
                {
                    RotateDuration = facingDuration,
                    RotateEase = facingEase,
                    YAxisOnly = facingYAxisOnly,
                    InstantThresholdAngle = facingInstantThresholdAngle
                };

                bool started = SmoothFacingTween.TryRotateToWorldPosition(
                    transform,
                    npcRoot.position,
                    ref _activeFacingTween,
                    options,
                    () =>
                {
                    _activeFacingTween = null;
                    _isWaitingFacingComplete = false;
                    PerformInteraction(target, targetObject);
                });

                // 若目标位置无效导致未启动 Tween，立即回退执行交互，避免输入“吞掉”。
                if (!started)
                {
                    _isWaitingFacingComplete = false;
                    PerformInteraction(target, targetObject);
                }
                return;
            }

            PerformInteraction(target, targetObject);
        }

        /// <summary>
        /// 统一交互执行出口：
        /// 负责真正调用 IInteractable，并维护冷却与事件广播。
        /// </summary>
        private void PerformInteraction(IInteractable target, GameObject targetObject)
        {
            if (target == null || targetObject == null) return;

            target.Interact(gameObject);
            _nextAllowedInteractTime = Time.unscaledTime + Mathf.Max(0f, interactCooldown);

            // 广播一次“交互已执行”事件，便于埋点、音效、提示系统解耦监听。
            EventBus.Raise(new PlayerInteractionPerformedEvent
            {
                Interactor = gameObject,
                Target = targetObject
            });
        }

        /// <summary>
        /// 判断目标是否属于 NPC，并返回 NPC 根节点 Transform。
        /// </summary>
        private static bool TryGetNpcRootTransform(GameObject rawTargetObject, out Transform npcRootTransform)
        {
            npcRootTransform = null;
            if (rawTargetObject == null) return false;

            // 目标对象可能是 NPC 的子物体（例如触发器碰撞体）；
            // 所以这里向父级查找 NPCInteractable，确保拿到 NPC 根节点。
            NPCInteractable npc = rawTargetObject.GetComponentInParent<NPCInteractable>();
            if (npc == null) return false;

            npcRootTransform = npc.transform;
            return npcRootTransform != null;
        }

    }
}
