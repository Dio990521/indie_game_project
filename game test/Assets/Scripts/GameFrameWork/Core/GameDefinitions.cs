namespace IndieGame.Core
{
    /// <summary>
    /// 全局游戏状态枚举
    /// </summary>
    public enum GameState
    {
        FreeRoam,   // 自由移动模式 (塞尔达风格)
        BoardMode,  // 棋盘模式 (马里奥派对风格)
        Dialogue,   // 对话模式 (禁止所有移动)
        Paused      // 暂停
    }
}