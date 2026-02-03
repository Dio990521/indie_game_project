using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IndieGame.Gameplay.Camp;
using IndieGame.Core;

namespace IndieGame.UI.Camp
{
    /// <summary>
    /// 露营 UI 绑定器：
    /// 负责保存按钮预制体与布局容器，并根据解锁动作动态生成按钮。
    /// </summary>
    public class CampUIBinder : MonoBehaviour
    {
        [Header("Layout")]
        // 垂直布局容器（用于动态摆放按钮）
        [SerializeField] private VerticalLayoutGroup menuContainer;
        // 按钮预制体（建议包含 Button + TMP_Text）
        [SerializeField] private GameObject actionButtonPrefab;
        // 协程宿主（用于 Sleep 的延迟逻辑）
        private MonoBehaviour _coroutineHost;

        /// <summary>
        /// 动态初始化菜单：
        /// 传入已解锁动作列表，自动生成按钮。
        /// </summary>
        public void InitializeMenu(List<CampActionSO> unlockedActions)
        {
            if (menuContainer == null || actionButtonPrefab == null) return;
            if (_coroutineHost == null) _coroutineHost = this;

            // 清空旧按钮
            for (int i = menuContainer.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(menuContainer.transform.GetChild(i).gameObject);
            }

            if (unlockedActions == null) return;

            for (int i = 0; i < unlockedActions.Count; i++)
            {
                CampActionSO action = unlockedActions[i];
                if (action == null) continue;

                // 实例化按钮并挂到容器
                GameObject buttonObj = Instantiate(actionButtonPrefab, menuContainer.transform);
                // 绑定显示文本
                TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = action.DisplayName;
                }

                // 绑定点击事件
                Button btn = buttonObj.GetComponentInChildren<Button>(true);
                if (btn != null)
                {
                    btn.onClick.AddListener(() => HandleActionClick(action));
                }
            }
        }

        /// <summary>
        /// 动作按钮点击逻辑：
        /// 目前仅输出日志，保留扩展入口。
        /// </summary>
        private void HandleActionClick(CampActionSO action)
        {
            if (action == null) return;
            switch (action.ActionID)
            {
                case CampActionID.Crafting:
                    Debug.Log("Log: 消耗大量时间，开启制作...");
                    break;
                case CampActionID.Inventory:
                    Debug.Log("Log: 打开已有 Inventory 界面...");
                    break;
                case CampActionID.Memory:
                    Debug.Log("Log: 检索语料库，查看任务记录与对话日志...");
                    break;
                case CampActionID.SkillTree:
                    Debug.Log("Log: 打开技能配置界面...");
                    break;
                case CampActionID.ShopManagement:
                    Debug.Log("Log: 极少量消耗时间，飞鸽传书远程管理...");
                    break;
                case CampActionID.Sleep:
                    Debug.Log("Log: 根据剩余时间计算翌日状态，执行黑屏转场退出露营...");
                    if (_coroutineHost != null)
                    {
                        _coroutineHost.StartCoroutine(SleepRoutine());
                    }
                    break;
            }
        }

        private IEnumerator SleepRoutine()
        {
            // 1) 黑屏淡入
            EventBus.Raise(new FadeRequestedEvent { FadeIn = true, Duration = 1f });
            // 2) 延迟 1 秒后返回棋盘
            yield return new WaitForSeconds(1f);
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.ReturnToBoard();
            }
        }
    }
}
