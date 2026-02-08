using System;

namespace IndieGame.Gameplay.Crafting
{
    /// <summary>
    /// 图纸运行时记录：
    /// 该类负责保存“与玩家进度相关”的动态状态，不保存静态配方内容。
    ///
    /// 字段职责：
    /// - ID：指向 BlueprintSO 的唯一键
    /// - IsConsumed：是否已被消耗（一次性图纸）
    /// </summary>
    [Serializable]
    public class BlueprintRecord
    {
        /// <summary> 图纸 ID（必须与 BlueprintSO.ID 对齐） </summary>
        public string ID;

        /// <summary>
        /// 是否已被消耗：
        /// true 代表该图纸不应再出现在可制造列表中。
        /// </summary>
        public bool IsConsumed;

        /// <summary>
        /// 默认构造（供序列化反序列化使用）
        /// </summary>
        public BlueprintRecord()
        {
            ID = string.Empty;
            IsConsumed = false;
        }

        /// <summary>
        /// 常用构造：创建新记录时直接写入图纸 ID。
        /// </summary>
        public BlueprintRecord(string id)
        {
            ID = id;
            IsConsumed = false;
        }
    }
}
