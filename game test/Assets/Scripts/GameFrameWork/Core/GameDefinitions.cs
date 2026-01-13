namespace IndieGame.Core
{
    public enum GameState
    {
        FreeRoam,        // 自由探索
        BoardMode,       // 棋盘移动中 (自动跑)
        TurnDecision,    // ⚡新增：回合内决策 (例如：选岔路、选攻击目标)
        Dialogue,        // 对话
        Paused           // 暂停
    }
}