using System.Collections;
using UnityEngine;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.Core.Utilities;
using IndieGame.Gameplay.ActionPoint;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 控制器（Controller / Manager）：
    /// <para>
    /// 负责"露营 → 睡觉"流程的业务编排：黑屏 → 恢复行动点 → 推进日期 → 自动存档 →
    /// 隐藏菜单 → 返回棋盘 → 黑屏淡出。同时管理流程互斥。
    /// </para>
    /// <para>
    /// MVB 边界说明：
    /// - View 仅负责显示/隐藏菜单与转发按钮点击事件（Sleep 转发为 CampSleepRequestedEvent）；
    /// - Controller（本类）负责所有跨系统编排（ActionPointSystem / DateSystem / SceneLoader / AutoSaveService）；
    /// - View 的引用通过 SerializeField 注入，仅用于在流程结束时调用 Hide() 隐藏菜单。
    /// </para>
    /// <para>
    /// 重构说明：原先"发起自动存档请求并等待完成"的 ~90 行样板（请求序号、pending 字段、
    /// 超时轮询、RequestId 匹配回调）已抽取为可复用的 <see cref="AutoSaveAwaiter"/>，
    /// 与 TownUIController 共用同一实现。
    /// </para>
    /// </summary>
    public class CampUIController : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("受控的 CampUIView 引用，流程末尾会调用其 Hide() 隐藏菜单。")]
        [SerializeField] private CampUIView view;

        [Header("Sleep Auto Save")]
        [Tooltip("是否在执行 Sleep 时自动触发一次存档。")]
        [SerializeField] private bool enableSleepAutoSave = true;

        [Tooltip("Sleep 自动存档写入槽位。")]
        [SerializeField] private int sleepAutoSaveSlotIndex = 0;

        [Tooltip("Sleep 自动存档备注（用于标题读档列表识别该存档来源）。")]
        [SerializeField] private string sleepAutoSaveNote = "AutoSave-Sleep";

        [Tooltip("等待自动存档完成的超时时长（秒）。超时后会继续返回棋盘，避免流程卡死。")]
        [SerializeField] private float sleepAutoSaveTimeoutSeconds = 8f;

        // 自动存档请求/等待/匹配的复用工具（替代原先散落在本类的 ~90 行样板）。
        private readonly AutoSaveAwaiter _autoSaveAwaiter = new AutoSaveAwaiter("[CampUIController] Sleep");

        // 当前 Sleep 协程引用（非 null 即代表流程正在进行）。
        // 用于防止玩家在黑屏淡入期间快速重复点击 Sleep 按钮触发并行流程。
        private Coroutine _sleepCoroutine;

        private void OnEnable()
        {
            // 订阅露营 Sleep 业务请求（由 View 在按钮点击后发布）。
            EventBus.Subscribe<CampSleepRequestedEvent>(HandleSleepRequested);
            _autoSaveAwaiter.Subscribe();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CampSleepRequestedEvent>(HandleSleepRequested);
            // 退订并复位等待状态，避免协程被 Stop 后旧 RequestId 在下次流程中错误匹配。
            _autoSaveAwaiter.Unsubscribe();

            // 兜底：组件被禁用/销毁时若 Sleep 协程仍在跑，强制停止并清理状态，
            // 避免下次启用时残留的 _sleepCoroutine 引用让互斥保护误判。
            if (_sleepCoroutine != null)
            {
                StopCoroutine(_sleepCoroutine);
                _sleepCoroutine = null;
            }
        }

        /// <summary>
        /// 处理"睡觉"业务请求：
        /// 互斥检查通过后启动 SleepRoutine 协程。
        /// </summary>
        private void HandleSleepRequested(CampSleepRequestedEvent evt)
        {
            if (_sleepCoroutine != null)
            {
                DebugTools.LogWarning("[CampUIController] Sleep 流程已在进行中，忽略重复请求。");
                return;
            }
            _sleepCoroutine = StartCoroutine(SleepRoutine());
        }

        /// <summary>
        /// 睡觉流程协程：
        /// 1) 黑屏淡入；2) 恢复行动点 + 推进日期；3) 自动存档（带超时）；
        /// 4) 隐藏露营菜单；5) 返回棋盘；6) 黑屏淡出 + 通知 GameManager 结束加载。
        /// </summary>
        private IEnumerator SleepRoutine()
        {
            float fadeDuration = 1f;

            // 1) 黑屏淡入：通过 EventBus 通知全局淡入淡出 UI 负责执行。
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = fadeDuration });
            yield return new WaitForSecondsRealtime(fadeDuration);

            // 2) 睡觉结算：恢复全部行动点，推进游戏日期。
            ActionPointSystem.Instance?.RefillActionPoints("Sleep");
            IndieGame.Gameplay.Date.DateSystem.Instance?.AdvanceDay();

            // 3) 在返回棋盘前执行一次自动存档（超时/失败的告警日志由 Awaiter 统一输出）。
            if (enableSleepAutoSave)
            {
                yield return _autoSaveAwaiter.RequestAndWait(
                    AutoSaveReason.Sleep, sleepAutoSaveSlotIndex, sleepAutoSaveNote, sleepAutoSaveTimeoutSeconds);
            }

            // 4) 关闭露营 UI（避免与棋盘 UI 叠加）。
            if (view != null)
            {
                view.Hide();
            }

            // 5) 返回棋盘（不重复触发淡入淡出，由本流程统一控制）。
            if (SceneLoader.Instance != null)
            {
                yield return SceneLoader.Instance.ReturnToBoardRoutine(false, fadeDuration, false);
            }

            // 6) 黑屏淡出（等待棋盘加载完成后执行）。
            EventBus.Raise(new FadeRequestedEvent { FadeIn = false, Duration = fadeDuration });
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndLoading();
            }

            // 流程正常结束：清空互斥标记，允许下一次 Sleep。
            // 协程被 OnDisable 中途 Stop 的情况由 OnDisable 兜底清理。
            _sleepCoroutine = null;
        }
    }
}
