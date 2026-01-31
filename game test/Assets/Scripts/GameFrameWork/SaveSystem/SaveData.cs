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
        // 存档时间（UTC 字符串，便于跨时区显示）
        public string SavedAtUtc;
        // 备注（可选）
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
