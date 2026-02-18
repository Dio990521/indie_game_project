using IndieGame.Core;
using IndieGame.Gameplay.Dialogue;
using Unity.Cinemachine;
using UnityEngine;

namespace IndieGame.Core.CameraSystem
{
    /// <summary>
    /// 对话镜头控制器（Cinemachine 3.x）：
    /// 挂在“对话专用 CinemachineCamera”物体上，
    /// 通过监听对话开始/结束事件来切换镜头优先级，实现：
    /// - 对话开始：切到特写镜头
    /// - 对话结束：切回主镜头
    ///
    /// 设计说明：
    /// 1) 默认使用 Priority 切换，而不是 SetActive 开关。
    ///    原因：若直接把自身 GameObject 设为 inactive，会导致脚本退订事件，下一次对话无法自动再启用。
    /// 2) 可选支持“自动目标绑定”：
    ///    - 优先使用最近一次玩家交互事件里的 NPC 目标
    ///    - 若拿不到，再回退到 Inspector 指定的 Follow/LookAt
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera))]
    public class DialogueCameraController : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("对话专用 CinemachineCamera；不填则自动取同物体组件")]
        [SerializeField] private CinemachineCamera dialogueCamera;

        [Header("Priority Switch")]
        [Tooltip("对话镜头激活时的 Priority（需高于主镜头）")]
        [SerializeField] private int dialogueActivePriority = 50;
        [Tooltip("对话镜头闲置时的 Priority（建议低于主镜头）")]
        [SerializeField] private int dialogueInactivePriority = 0;

        [Header("Optional Dynamic Target")]
        [Tooltip("是否在对话开始时自动设置 Follow/LookAt 目标")]
        [SerializeField] private bool autoAssignTargets = true;
        [Tooltip("Follow 回退目标（当无法从最近交互拿到 NPC 时使用）")]
        [SerializeField] private Transform fallbackFollowTarget;
        [Tooltip("LookAt 回退目标（当无法从最近交互拿到 NPC 时使用）")]
        [SerializeField] private Transform fallbackLookAtTarget;
        [Tooltip("LookAt 优先看向交互发起者（通常是玩家）")]
        [SerializeField] private bool preferLookAtInteractor = false;

        // 最近一次“玩家执行交互”事件缓存：
        // 用于在对话开始瞬间动态定位镜头关注目标。
        private Transform _lastInteractor;
        private Transform _lastNpcTarget;

        private void Awake()
        {
            if (dialogueCamera == null)
            {
                dialogueCamera = GetComponent<CinemachineCamera>();
            }

            // 启动时强制设为闲置优先级，避免场景加载时误抢主镜头。
            ApplyPriority(isDialogueActive: false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(HandleDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(HandleDialogueEnded);
            EventBus.Subscribe<PlayerInteractionPerformedEvent>(HandlePlayerInteractionPerformed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(HandleDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(HandleDialogueEnded);
            EventBus.Unsubscribe<PlayerInteractionPerformedEvent>(HandlePlayerInteractionPerformed);
        }

        /// <summary>
        /// 记录最近一次交互双方：
        /// 便于对话开始时把镜头自动对准当前 NPC。
        /// </summary>
        private void HandlePlayerInteractionPerformed(PlayerInteractionPerformedEvent evt)
        {
            _lastInteractor = evt.Interactor != null ? evt.Interactor.transform : null;

            _lastNpcTarget = null;
            if (evt.Target == null) return;

            // 仅当目标确实是 NPCInteractable 时，才作为对话镜头目标缓存。
            if (evt.Target.GetComponentInParent<NPCInteractable>() != null)
            {
                _lastNpcTarget = evt.Target.transform;
            }
        }

        /// <summary>
        /// 对话开始：切到对话镜头（Priority 提升），并按需更新目标。
        /// </summary>
        private void HandleDialogueStarted(DialogueStartedEvent evt)
        {
            if (dialogueCamera == null) return;

            if (autoAssignTargets)
            {
                AssignTargetsForDialogue();
            }

            ApplyPriority(isDialogueActive: true);
        }

        /// <summary>
        /// 对话结束：降低对话镜头优先级，让主镜头重新生效。
        /// </summary>
        private void HandleDialogueEnded(DialogueEndedEvent evt)
        {
            ApplyPriority(isDialogueActive: false);
        }

        /// <summary>
        /// 根据缓存交互对象与回退配置，为对话镜头设置 Follow/LookAt。
        /// </summary>
        private void AssignTargetsForDialogue()
        {
            if (dialogueCamera == null) return;

            // Follow 策略：优先最近交互 NPC，其次 Inspector 回退目标
            Transform follow = _lastNpcTarget != null ? _lastNpcTarget : fallbackFollowTarget;
            // LookAt 策略：
            // - 若偏好看向交互者，则优先玩家
            // - 否则优先看向 NPC
            Transform lookAt;
            if (preferLookAtInteractor)
            {
                lookAt = _lastInteractor != null
                    ? _lastInteractor
                    : (_lastNpcTarget != null ? _lastNpcTarget : fallbackLookAtTarget);
            }
            else
            {
                lookAt = _lastNpcTarget != null
                    ? _lastNpcTarget
                    : (_lastInteractor != null ? _lastInteractor : fallbackLookAtTarget);
            }

            if (follow != null) dialogueCamera.Follow = follow;
            if (lookAt != null) dialogueCamera.LookAt = lookAt;
        }

        /// <summary>
        /// 统一设置对话镜头 Priority。
        /// </summary>
        private void ApplyPriority(bool isDialogueActive)
        {
            if (dialogueCamera == null) return;
            dialogueCamera.Priority = isDialogueActive ? dialogueActivePriority : dialogueInactivePriority;
        }
    }
}
