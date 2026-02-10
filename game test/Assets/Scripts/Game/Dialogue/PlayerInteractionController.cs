using IndieGame.Core;
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

        // 下一次允许触发交互的时间
        private float _nextAllowedInteractTime;

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

            target.Interact(gameObject);
            _nextAllowedInteractTime = Time.unscaledTime + Mathf.Max(0f, interactCooldown);

            // 广播一次“交互已执行”事件，便于埋点、音效、提示系统解耦监听。
            EventBus.Raise(new PlayerInteractionPerformedEvent
            {
                Interactor = gameObject,
                Target = targetObject
            });
        }
    }
}
