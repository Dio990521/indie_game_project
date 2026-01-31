using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 存档槽位 UI：
    /// 负责显示槽位信息、切换有档/无档状态，并提供按钮触发入口。
    /// </summary>
    public class SaveSlotUI : MonoBehaviour
    {
        // ---------------------------
        // Inspector 配置指南：
        // 1) SlotIndex：
        //    在 Inspector 中设置 SlotIndex（0 / 1 / 2）。
        // 2) 有档/无档状态切换：
        //    通过 hasSaveState / emptyState 绑定两个 GameObject，
        //    SaveSlotUI 会根据存档是否存在自动切换显隐。
        // 3) 动态文本：
        //    将 metaLabel 绑定到 TextMeshProUGUI，
        //    用于显示最后存档时间或备注。
        // 4) 按钮触发：
        //    通过 onSaveClicked / onLoadClicked 绑定按钮事件，
        //    可直接在 Inspector 里拖拽绑定 SaveManager.SaveAsync / LoadAsync。
        // ---------------------------

        [Header("Slot")]
        // 槽位索引（0/1/2）
        [SerializeField] private int slotIndex = 0;

        [Header("State Objects")]
        // 有存档时显示的物体
        [SerializeField] private GameObject hasSaveState;
        // 无存档时显示的物体
        [SerializeField] private GameObject emptyState;

        [Header("Meta Text")]
        // 显示最后保存时间/备注的文本
        [SerializeField] private TMP_Text metaLabel;

        [Header("Button Events")]
        // Inspector 可直接绑定 SaveSlotUI.Save
        public UnityEvent onSaveClicked;
        // Inspector 可直接绑定 SaveSlotUI.Load
        public UnityEvent onLoadClicked;

        private void OnEnable()
        {
            // 启用时刷新一次 UI 状态
            Refresh();
        }

        /// <summary>
        /// 刷新 UI：根据存档是否存在切换显示，并更新元数据文本。
        /// </summary>
        public void Refresh()
        {
            SaveManager manager = SaveManager.Instance;
            if (manager == null) return;

            bool hasSave = manager.HasSlot(slotIndex);
            if (hasSaveState != null) hasSaveState.SetActive(hasSave);
            if (emptyState != null) emptyState.SetActive(!hasSave);

            if (metaLabel != null)
            {
                if (!hasSave)
                {
                    // 无存档时显示占位文本
                    metaLabel.text = "No Save";
                    return;
                }

                SaveMetaData meta = manager.GetSlotMeta(slotIndex);
                if (meta == null || string.IsNullOrEmpty(meta.SavedAtUtc))
                {
                    // 元数据缺失时显示 Unknown
                    metaLabel.text = "Unknown";
                    return;
                }

                // 优先显示备注，其次显示时间
                metaLabel.text = !string.IsNullOrEmpty(meta.Note)
                    ? $"{meta.SavedAtUtc}\n{meta.Note}"
                    : meta.SavedAtUtc;
            }
        }

        /// <summary>
        /// 供按钮绑定：触发保存。
        /// </summary>
        public async void Save()
        {
            SaveManager manager = SaveManager.Instance;
            if (manager == null) return;
            // 调用异步存档
            await manager.SaveAsync(slotIndex);
            // 存档完成后刷新 UI
            Refresh();
        }

        /// <summary>
        /// 供按钮绑定：触发读取。
        /// </summary>
        public async void Load()
        {
            SaveManager manager = SaveManager.Instance;
            if (manager == null) return;
            // 调用异步读档
            await manager.LoadAsync(slotIndex);
            // 读档完成后刷新 UI
            Refresh();
        }
    }
}
