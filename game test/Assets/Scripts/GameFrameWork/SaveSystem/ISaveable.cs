using System;

namespace IndieGame.Core.SaveSystem
{
    /// <summary>
    /// 存档接口：
    /// 任意需要参与存档的模块只需实现此接口，并在 OnEnable/Start 注册到 SaveManager。
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// 唯一标识：用于在存档中定位该模块的数据。
        /// 建议使用稳定且不随场景变化的字符串（如 "PlayerStats"）。
        /// </summary>
        string SaveID { get; }

        /// <summary>
        /// 捕获当前状态并返回可序列化的数据对象。
        /// 建议返回 [Serializable] 的 POCO 类。
        /// </summary>
        object CaptureState();

        /// <summary>
        /// 恢复状态。
        /// data 的实际类型由 CaptureState 返回的类型决定。
        /// </summary>
        void RestoreState(object data);
    }
}
