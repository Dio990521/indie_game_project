using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using IndieGame.Core.Utilities;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 存档管理器（单例）：
    /// - 统一注册/管理 ISaveable
    /// - 负责存档/读档的 JSON IO
    /// - 管理槽位元数据（时间/备注）
    /// </summary>
    public class SaveManager : MonoSingleton<SaveManager>
    {
        // 存档文件夹相对路径
        [SerializeField] private string saveFolderName = "saves";

        // 当前已注册的存档模块
        private readonly List<ISaveable> _saveables = new List<ISaveable>();

        /// <summary>
        /// 注册存档模块（建议在 OnEnable/Start 调用）。
        /// </summary>
        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;
            if (_saveables.Contains(saveable)) return;
            _saveables.Add(saveable);
        }

        /// <summary>
        /// 注销存档模块（建议在 OnDisable/OnDestroy 调用）。
        /// </summary>
        public void Unregister(ISaveable saveable)
        {
            if (saveable == null) return;
            _saveables.Remove(saveable);
        }

        /// <summary>
        /// 保存到指定槽位（异步 IO）。
        /// </summary>
        public async Task SaveAsync(int slotIndex, string note = null)
        {
            EventBus.Raise(new SaveStartedEvent { SlotIndex = slotIndex });
            try
            {
                // 保证存档目录存在
                EnsureSaveFolder();
                // 采集所有模块的状态
                SaveData data = CaptureAll(note);
                // 序列化为 JSON（可读格式便于调试）
                string json = JsonUtility.ToJson(data, true);
                string path = GetSlotPath(slotIndex);

                // 后台线程执行写入，避免阻塞主线程
                await Task.Run(() => File.WriteAllText(path, json));

                EventBus.Raise(new SaveCompletedEvent { SlotIndex = slotIndex, Success = true });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Save failed: {ex}");
                EventBus.Raise(new SaveCompletedEvent
                {
                    SlotIndex = slotIndex,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// 从指定槽位读档（异步 IO）。
        /// </summary>
        public async Task LoadAsync(int slotIndex)
        {
            EventBus.Raise(new LoadStartedEvent { SlotIndex = slotIndex });
            try
            {
                string path = GetSlotPath(slotIndex);
                if (!File.Exists(path))
                {
                    EventBus.Raise(new LoadFailedEvent { SlotIndex = slotIndex, Error = "Save file not found." });
                    return;
                }

                // 后台线程读取 JSON
                string json = await Task.Run(() => File.ReadAllText(path));
                // 反序列化为 SaveData
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.StateData == null)
                {
                    EventBus.Raise(new LoadFailedEvent { SlotIndex = slotIndex, Error = "Invalid save data." });
                    return;
                }

                // 在主线程恢复所有模块状态
                RestoreAll(data);
                EventBus.Raise(new LoadCompletedEvent { SlotIndex = slotIndex, Success = true });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Load failed: {ex}");
                EventBus.Raise(new LoadFailedEvent { SlotIndex = slotIndex, Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取槽位元数据（用于 UI 显示）。
        /// </summary>
        public SaveMetaData GetSlotMeta(int slotIndex)
        {
            try
            {
                string path = GetSlotPath(slotIndex);
                if (!File.Exists(path)) return null;
                // 仅反序列化元数据用于 UI 展示
                string json = File.ReadAllText(path);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                return data != null ? data.MetaData : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Read meta failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 判断槽位是否存在存档文件。
        /// </summary>
        public bool HasSlot(int slotIndex)
        {
            return File.Exists(GetSlotPath(slotIndex));
        }

        private SaveData CaptureAll(string note)
        {
            SaveData data = new SaveData();
            data.MetaData.SavedAtUtc = DateTime.UtcNow.ToString("o");
            data.MetaData.Note = note;

            // 遍历所有已注册模块，逐个捕获状态
            for (int i = 0; i < _saveables.Count; i++)
            {
                ISaveable saveable = _saveables[i];
                if (saveable == null) continue;
                object state = saveable.CaptureState();
                if (state == null) continue;

                Type type = state.GetType();
                string json = JsonUtility.ToJson(state);
                SaveEntry entry = new SaveEntry
                {
                    SaveID = saveable.SaveID,
                    TypeName = type.AssemblyQualifiedName,
                    Json = json
                };
                data.StateData.Add(entry);
            }
            return data;
        }

        private void RestoreAll(SaveData data)
        {
            // 构建索引，加速查找
            Dictionary<string, SaveEntry> lookup = new Dictionary<string, SaveEntry>();
            for (int i = 0; i < data.StateData.Count; i++)
            {
                SaveEntry entry = data.StateData[i];
                if (entry == null || string.IsNullOrEmpty(entry.SaveID)) continue;
                lookup[entry.SaveID] = entry;
            }

            // 按注册顺序恢复各模块
            for (int i = 0; i < _saveables.Count; i++)
            {
                ISaveable saveable = _saveables[i];
                if (saveable == null) continue;
                if (!lookup.TryGetValue(saveable.SaveID, out SaveEntry entry)) continue;

                Type type = Type.GetType(entry.TypeName);
                if (type == null)
                {
                    Debug.LogWarning($"[SaveManager] Missing type for SaveID: {entry.SaveID}");
                    continue;
                }

                object state = JsonUtility.FromJson(entry.Json, type);
                saveable.RestoreState(state);
            }
        }

        private void EnsureSaveFolder()
        {
            string dir = GetSaveFolderPath();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private string GetSaveFolderPath()
        {
            return Path.Combine(Application.persistentDataPath, saveFolderName);
        }

        private string GetSlotPath(int slotIndex)
        {
            return Path.Combine(GetSaveFolderPath(), $"slot_{slotIndex}.json");
        }
    }
}
