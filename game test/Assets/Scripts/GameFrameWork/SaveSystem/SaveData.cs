using System;
using System.Collections.Generic;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 存档根数据：
    /// MetaData 保存存档元信息，StateData 保存模块状态列表。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // 元数据（存档时间、备注）
        public SaveMetaData MetaData = new SaveMetaData();
        // 各模块状态列表
        public List<SaveEntry> StateData = new List<SaveEntry>();
    }

    /// <summary>
    /// 存档元数据：
    /// 用于 UI 展示，例如存档时间/备注。
    /// </summary>
    [Serializable]
    public class SaveMetaData
    {
        // ---------------------------
        // 兼容说明：
        // 1) Timestamp / SceneName / PlayTime 是“标题读取列表”优先使用的新字段。
        // 2) SavedAtUtc / Note 保留用于兼容旧存档与旧 UI 逻辑，避免历史数据读取失败。
        // ---------------------------

        // 存档时间（用于列表展示，建议写入本地时间可读字符串）
        public string Timestamp;
        // 存档时所在场景名（用于标题界面快速识别进度位置）
        public string SceneName;
        // 游戏时长（秒，浮点；UI 层可格式化为 hh:mm:ss）
        public float PlayTime;

        // 旧字段：保留兼容
        public string SavedAtUtc;
        // 旧字段：保留兼容
        public string Note;
    }

    /// <summary>
    /// 单个模块的存档条目。
    /// </summary>
    [Serializable]
    public class SaveEntry
    {
        // 模块唯一 ID（ISaveable.SaveID）
        public string SaveID;
        // 数据类型（AssemblyQualifiedName）
        public string TypeName;
        // JSON 序列化后的内容
        public string Json;
    }
}
