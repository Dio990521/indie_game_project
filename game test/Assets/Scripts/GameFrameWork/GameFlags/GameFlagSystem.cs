using System;
using System.Collections.Generic;
using IndieGame.Core.SaveSystem;
using IndieGame.Core.Utilities;

namespace IndieGame.Core
{
    /// <summary>
    /// 全局事件标志数据库（单例 + 存档）：
    /// 以字符串 Key 为索引，存储布尔型游戏标志（如任务是否完成、门是否已开等）。
    ///
    /// 设计说明：
    /// - 作为单一事实来源（Single Source of Truth），所有系统通过此类读写游戏进度状态；
    /// - 每次 SetFlag 若值发生变化则广播 GameFlagChangedEvent，支持响应式解耦；
    /// - 实现 ISaveable，存档读档时自动序列化/恢复全部标志；
    /// - 后续可扩展整数型计数器（任务阶段、收集数量等），接口不变。
    ///
    /// 使用示例：
    /// <code>
    /// // 设置标志（任务完成、开门等）
    /// GameFlagSystem.Instance.SetFlag("village_gate_opened", true);
    ///
    /// // 读取标志（检查障碍物状态、任务进度等）
    /// bool isOpen = GameFlagSystem.Instance.GetFlag("village_gate_opened");
    /// </code>
    /// </summary>
    public class GameFlagSystem : SaveableMonoSingleton<GameFlagSystem>
    {
        // ── 存档 ─────────────────────────────────────────────────────────────

        public override string SaveID => "GameFlagSystem";

        [Serializable]
        private struct FlagEntry
        {
            public string Key;
            public bool Value;
        }

        [Serializable]
        private class SaveState
        {
            public List<FlagEntry> Flags = new List<FlagEntry>();
        }

        public override object CaptureState()
        {
            var state = new SaveState();
            foreach (var kv in _flags)
                state.Flags.Add(new FlagEntry { Key = kv.Key, Value = kv.Value });
            return state;
        }

        public override void RestoreState(object data)
        {
            if (data is not SaveState s) return;
            _flags.Clear();
            foreach (var entry in s.Flags)
                _flags[entry.Key] = entry.Value;
        }

        // ── 运行时状态 ────────────────────────────────────────────────────────

        private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();

        // ── 公开接口 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 设置布尔标志。若值未发生变化则不广播事件（幂等）。
        /// </summary>
        /// <param name="key">标志唯一标识（建议 snake_case，如 "village_gate_opened"）</param>
        /// <param name="value">新值</param>
        public void SetFlag(string key, bool value)
        {
            if (string.IsNullOrEmpty(key)) return;

            // 值相同时不广播，避免无效刷新
            if (_flags.TryGetValue(key, out bool current) && current == value) return;

            _flags[key] = value;
            DebugTools.Log($"[GameFlagSystem] Flag 变更: {key} = {value}");
            EventBus.Raise(new GameFlagChangedEvent { Key = key, NewValue = value });
        }

        /// <summary>
        /// 读取布尔标志。Key 不存在时返回 defaultValue（默认 false）。
        /// </summary>
        public bool GetFlag(string key, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            return _flags.TryGetValue(key, out bool value) ? value : defaultValue;
        }

        /// <summary>
        /// 查询指定 Key 是否已被显式设置过（无论值为 true 还是 false）。
        /// </summary>
        public bool HasFlag(string key)
        {
            return !string.IsNullOrEmpty(key) && _flags.ContainsKey(key);
        }

        /// <summary>
        /// 返回当前所有 Flag 的只读快照（供编辑器 Inspector 展示使用）。
        /// </summary>
        public Dictionary<string, bool> GetAllFlags()
        {
            return _flags;
        }
    }
}
