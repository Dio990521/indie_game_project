using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        // 统一的槽位数量上限（用于标题界面遍历 slot_0 ~ slot_(N-1)）
        [SerializeField] private int maxSlotCount = 10;

        // 当前已注册的存档模块
        private readonly List<ISaveable> _saveables = new List<ISaveable>();
        // 最近一次成功读档的数据快照（用于“延迟注册模块”的补恢复）
        private SaveData _lastLoadedData;
        // 是否存在有效读档快照
        private bool _hasLoadedData;

        // M3 修复：TypeName -> Type 的解析缓存，避免每次恢复都全程序集扫描
        private static readonly Dictionary<string, Type> TypeResolveCache = new Dictionary<string, Type>(StringComparer.Ordinal);

        // M4 修复：累计游玩时长（秒）。
        // 旧实现直接写 Time.realtimeSinceStartup，每次重启进程归零，读档再存会"时长倒退"。
        // 现改为：读档时恢复历史累计值，存档时写入 累计值 + 本次会话增量。
        private float _accumulatedPlayTime;
        private float _sessionStartRealtime;

        /// <summary>
        /// 注册存档模块（建议在 OnEnable/Start 调用）。
        /// </summary>
        public void Register(ISaveable saveable)
        {
            if (saveable == null) return;
            if (_saveables.Contains(saveable)) return;
            _saveables.Add(saveable);

            // 如果此前已经执行过 Load（例如标题界面先读档、随后才进入玩法场景注册模块），
            // 则在注册瞬间尝试把该模块的状态补恢复，确保“先读档后初始化”也能连通。
            TryRestoreSaveableFromLoadedData(saveable);
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
        /// <param name="sourceTag">
        /// 保存来源标签（可选）：
        /// - 用于事件监听方精准识别“这次 Save 是谁发起的”；
        /// - 典型值：AutoSaveService:Sleep:Request42。
        /// </param>
        public async Task SaveAsync(int slotIndex, string note = null, string sourceTag = null)
        {
            EventBus.Raise(new SaveStartedEvent
            {
                SlotIndex = slotIndex,
                SourceTag = sourceTag
            });
            try
            {
                // 保证存档目录存在
                EnsureSaveFolder();
                // 采集所有模块的状态
                SaveData data = CaptureAll(note);
                // 序列化为 JSON（可读格式便于调试）
                string json = JsonUtility.ToJson(data, true);
                string path = GetSlotPath(slotIndex);

                // H4 修复：原子写盘。
                // 旧实现直接覆写目标文件，写一半崩溃/断电会导致存档截断损坏。
                // 现改为：先写 .tmp，再用 File.Replace 原子替换（同时把上一份好档留为 .bak）。
                await Task.Run(() =>
                {
                    string tmpPath = path + ".tmp";
                    File.WriteAllText(tmpPath, json);
                    if (File.Exists(path))
                    {
                        // 原子替换：目标文件要么是旧的完整存档，要么是新的完整存档
                        File.Replace(tmpPath, path, path + ".bak");
                    }
                    else
                    {
                        File.Move(tmpPath, path);
                    }
                });

                EventBus.Raise(new SaveCompletedEvent
                {
                    SlotIndex = slotIndex,
                    Success = true,
                    SourceTag = sourceTag
                });
            }
            catch (Exception ex)
            {
                DebugTools.LogError($"[SaveManager] Save failed: {ex}");
                EventBus.Raise(new SaveCompletedEvent
                {
                    SlotIndex = slotIndex,
                    Success = false,
                    Error = ex.Message,
                    SourceTag = sourceTag
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

                // 缓存本次成功解析的完整数据：
                // 供后续“晚注册”的 ISaveable 模块在 Register 阶段自动补恢复。
                _lastLoadedData = data;
                _hasLoadedData = true;

                // M4 修复：恢复历史累计游玩时长，并以当前时刻作为本次会话计时起点
                _accumulatedPlayTime = Mathf.Max(0f, data.MetaData != null ? data.MetaData.PlayTime : 0f);
                _sessionStartRealtime = Time.realtimeSinceStartup;

                // 在主线程恢复所有模块状态
                RestoreAll(data);
                EventBus.Raise(new LoadCompletedEvent { SlotIndex = slotIndex, Success = true });
            }
            catch (Exception ex)
            {
                DebugTools.LogError($"[SaveManager] Load failed: {ex}");
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
                DebugTools.LogWarning($"[SaveManager] Read meta failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取全部槽位元数据：
        /// - 固定遍历 0 ~ (maxSlotCount - 1)
        /// - 有存档返回对应 MetaData
        /// - 无存档返回 null（调用方可据此显示 Empty）
        ///
        /// 返回约定：
        /// - 返回列表长度恒定为 maxSlotCount
        /// - 列表索引即槽位索引（index == slotIndex）
        /// </summary>
        public List<SaveMetaData> GetAllSaveSlots()
        {
            List<SaveMetaData> result = new List<SaveMetaData>(Mathf.Max(0, maxSlotCount));

            for (int slotIndex = 0; slotIndex < Mathf.Max(0, maxSlotCount); slotIndex++)
            {
                // 直接复用已有单槽位读取入口，保持行为一致
                SaveMetaData meta = GetSlotMeta(slotIndex);
                result.Add(meta);
            }

            return result;
        }

        /// <summary>
        /// 判断槽位是否存在存档文件。
        /// </summary>
        public bool HasSlot(int slotIndex)
        {
            return File.Exists(GetSlotPath(slotIndex));
        }

        /// <summary>
        /// 清空“最近一次读档缓存”：
        /// 典型用途是在标题界面点击 New Game 时调用，避免旧的读档快照在新游戏流程中被误应用。
        /// </summary>
        public void ClearLoadedStateCache()
        {
            _lastLoadedData = null;
            _hasLoadedData = false;
            // 新游戏：游玩时长从零开始计
            _accumulatedPlayTime = 0f;
            _sessionStartRealtime = Time.realtimeSinceStartup;
        }

        private SaveData CaptureAll(string note)
        {
            SaveData data = new SaveData();
            // ---------------------------
            // 元数据写入策略：
            // 1) 新字段：用于标题列表展示
            // 2) 旧字段：保留写入，兼容旧 UI / 旧存档读取逻辑
            // ---------------------------
            string nowLocalDisplay = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string nowUtcIso = DateTime.UtcNow.ToString("o");
            data.MetaData.Timestamp = nowLocalDisplay;
            data.MetaData.SceneName = SceneManager.GetActiveScene().name;
            // M4 修复：写入"历史累计 + 本次会话增量"，而非进程启动以来的裸时长
            data.MetaData.PlayTime = _accumulatedPlayTime + Mathf.Max(0f, Time.realtimeSinceStartup - _sessionStartRealtime);
            data.MetaData.SavedAtUtc = nowUtcIso;
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
                    // M3 修复：写入 FullName 而非 AssemblyQualifiedName。
                    // AQN 携带程序集名+版本号，加 asmdef / 改程序集名 / 升级 Unity 都会让旧档解析失败。
                    // FullName 只含命名空间+类型名，配合 ResolveStateType 的全程序集扫描可稳定解析。
                    TypeName = type.FullName,
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

                Type type = ResolveStateType(entry.TypeName);
                if (type == null)
                {
                    DebugTools.LogWarning($"[SaveManager] Missing type for SaveID: {entry.SaveID}");
                    continue;
                }

                object state = JsonUtility.FromJson(entry.Json, type);
                saveable.RestoreState(state);
            }
        }

        /// <summary>
        /// M3 修复：稳定的类型名解析。
        /// 1) 先查缓存（含负缓存，避免重复全程序集扫描）；
        /// 2) Type.GetType：可解析旧存档里的 AssemblyQualifiedName，以及本程序集/mscorlib 的 FullName；
        /// 3) 兜底遍历当前已加载的所有程序集按 FullName 查找（新格式跨程序集场景）。
        /// </summary>
        private static Type ResolveStateType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (TypeResolveCache.TryGetValue(typeName, out Type cached)) return cached;

            Type resolved = Type.GetType(typeName);
            if (resolved == null)
            {
                System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    resolved = assemblies[i].GetType(typeName);
                    if (resolved != null) break;
                }
            }

            // 无论成败都写入缓存（负缓存防止对损坏类型名反复扫描）
            TypeResolveCache[typeName] = resolved;
            return resolved;
        }

        /// <summary>
        /// 尝试用最近一次读档缓存，恢复“刚注册进来”的单个模块。
        /// 该能力用于解决读档时序问题：
        /// - 标题界面先触发 LoadAsync；
        /// - 玩法场景稍后初始化，ISaveable 才开始注册；
        /// - 注册瞬间可根据缓存补恢复，避免状态丢失。
        /// </summary>
        private void TryRestoreSaveableFromLoadedData(ISaveable saveable)
        {
            if (saveable == null) return;
            if (!_hasLoadedData || _lastLoadedData == null || _lastLoadedData.StateData == null) return;

            for (int i = 0; i < _lastLoadedData.StateData.Count; i++)
            {
                SaveEntry entry = _lastLoadedData.StateData[i];
                if (entry == null || string.IsNullOrEmpty(entry.SaveID)) continue;
                if (!string.Equals(entry.SaveID, saveable.SaveID, StringComparison.Ordinal)) continue;

                Type type = ResolveStateType(entry.TypeName);
                if (type == null)
                {
                    DebugTools.LogWarning($"[SaveManager] Missing type for SaveID: {entry.SaveID}");
                    return;
                }

                object state = JsonUtility.FromJson(entry.Json, type);
                saveable.RestoreState(state);
                return;
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
