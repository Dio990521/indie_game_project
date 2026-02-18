using System.Collections.Generic;
using IndieGame.Core;
using IndieGame.Core.SaveSystem;
using IndieGame.UI.Confirmation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IndieGame.UI.TitleScreen
{
    /// <summary>
    /// 标题界面读档菜单视图（UI 层）：
    /// 负责展示存档列表、处理点击槽位、弹出二次确认并发起 LoadAsync。
    ///
    /// 架构定位：
    /// - 视图层：只处理“展示与交互转发”，不保存核心游戏数据。
    /// - 通信方式：通过 EventBus 接收开关菜单事件，通过 ConfirmationEvent 发起确认弹窗。
    /// </summary>
    public class SaveLoadMenuView : MonoBehaviour
    {
        [Header("Binder")]
        [Tooltip("读档菜单 UI 绑定器（只存引用，不写逻辑）")]
        [SerializeField] private SaveLoadMenuBinder binder;

        // 运行时生成的按钮缓存，便于刷新时统一销毁
        private readonly List<Button> _spawnedButtons = new List<Button>();

        private void Awake()
        {
            if (binder == null)
            {
                Debug.LogError("[SaveLoadMenuView] Missing SaveLoadMenuBinder reference.");
                return;
            }

            if (binder.CloseButton != null)
            {
                binder.CloseButton.onClick.AddListener(HandleCloseClicked);
            }

            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (binder != null && binder.CloseButton != null)
            {
                binder.CloseButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OpenSaveLoadMenuEvent>(HandleOpenMenuEvent);
            EventBus.Subscribe<CloseSaveLoadMenuEvent>(HandleCloseMenuEvent);
            EventBus.Subscribe<LoadCompletedEvent>(HandleLoadCompletedEvent);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OpenSaveLoadMenuEvent>(HandleOpenMenuEvent);
            EventBus.Unsubscribe<CloseSaveLoadMenuEvent>(HandleCloseMenuEvent);
            EventBus.Unsubscribe<LoadCompletedEvent>(HandleLoadCompletedEvent);
        }

        /// <summary>
        /// 对外主入口：
        /// 读取 SaveManager 元数据并重建列表。
        /// </summary>
        public void ShowSaveList()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                Debug.LogWarning("[SaveLoadMenuView] SaveManager instance not found.");
                SetVisible(false);
                return;
            }

            RebuildSlotButtons(saveManager.GetAllSaveSlots());
            SetVisible(true);
        }

        /// <summary>
        /// 用元数据列表重建按钮。
        /// 列表 index 即 slotIndex。
        /// </summary>
        private void RebuildSlotButtons(List<SaveMetaData> allSlots)
        {
            ClearSpawnedButtons();

            if (binder == null || binder.ListContainer == null || binder.SlotButtonPrefab == null)
            {
                Debug.LogWarning("[SaveLoadMenuView] Binder.ListContainer or Binder.SlotButtonPrefab is not assigned.");
                return;
            }

            bool hasAnySave = false;
            int slotCount = allSlots != null ? allSlots.Count : 0;
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                SaveMetaData meta = allSlots[slotIndex];
                if (meta == null) continue; // 只为有档槽位生成按钮
                hasAnySave = true;

                Button button = Instantiate(binder.SlotButtonPrefab, binder.ListContainer);
                _spawnedButtons.Add(button);

                TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
                if (label != null)
                {
                    label.text = BuildSlotLabel(slotIndex, meta);
                }

                int capturedSlotIndex = slotIndex;
                if (button != null)
                {
                    button.onClick.AddListener(() => HandleSlotClicked(capturedSlotIndex));
                }
            }

            if (binder.EmptyStateNode != null)
            {
                binder.EmptyStateNode.SetActive(!hasAnySave);
                if (binder.EmptyStateText != null && !hasAnySave)
                {
                    // 若绑定了可选文本，则在空态时给出明确提示。
                    binder.EmptyStateText.text = "No Save Data";
                }
            }
        }

        /// <summary>
        /// 构造槽位显示文本。
        /// </summary>
        private static string BuildSlotLabel(int slotIndex, SaveMetaData meta)
        {
            string timestamp = string.IsNullOrWhiteSpace(meta.Timestamp) ? "Unknown Time" : meta.Timestamp;
            string sceneName = string.IsNullOrWhiteSpace(meta.SceneName) ? "Unknown Scene" : meta.SceneName;
            string playTime = FormatPlayTime(meta.PlayTime);
            return $"Slot {slotIndex}\n{timestamp}\nScene: {sceneName}\nPlay: {playTime}";
        }

        /// <summary>
        /// 把秒数格式化为 HH:MM:SS。
        /// </summary>
        private static string FormatPlayTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            return $"{h:00}:{m:00}:{s:00}";
        }

        /// <summary>
        /// 点击某个槽位后的处理：
        /// 先弹确认框，确认后执行异步读档。
        /// </summary>
        private void HandleSlotClicked(int slotIndex)
        {
            ConfirmationEvent.Request(new ConfirmationRequest
            {
                Message = $"Load Slot {slotIndex}?",
                OnConfirm = () => BeginLoadSlot(slotIndex),
                OnCancel = null
            });
        }

        private async void BeginLoadSlot(int slotIndex)
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null) return;

            // 通知标题主流程：“用户已经确认读取某个槽位”。
            // TitleScreenManager 会在收到后进入“等待 LoadCompletedEvent 后自动进游戏”的状态。
            EventBus.Raise(new TitleLoadGameRequestedEvent
            {
                SlotIndex = slotIndex
            });

            await saveManager.LoadAsync(slotIndex);
        }

        private void HandleOpenMenuEvent(OpenSaveLoadMenuEvent evt)
        {
            ShowSaveList();
        }

        private void HandleCloseMenuEvent(CloseSaveLoadMenuEvent evt)
        {
            SetVisible(false);
        }

        private void HandleLoadCompletedEvent(LoadCompletedEvent evt)
        {
            // 读档成功后自动关闭菜单，避免与后续场景 UI 重叠。
            SetVisible(false);
        }

        private void HandleCloseClicked()
        {
            EventBus.Raise(new CloseSaveLoadMenuEvent());
        }

        private void SetVisible(bool visible)
        {
            if (binder != null && binder.RootPanel != null)
            {
                binder.RootPanel.SetActive(visible);
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        private void ClearSpawnedButtons()
        {
            for (int i = 0; i < _spawnedButtons.Count; i++)
            {
                Button button = _spawnedButtons[i];
                if (button == null) continue;
                Destroy(button.gameObject);
            }

            _spawnedButtons.Clear();
        }
    }
}
