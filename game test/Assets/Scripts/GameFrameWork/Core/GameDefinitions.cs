namespace IndieGame.Core
{
    /// <summary>
    /// 游戏状态（偏逻辑层）：
    /// 由 GameManager 维护，代表当前游戏运行所处的宏观阶段。
    /// </summary>
    public enum GameState
    {
        // 初始化阶段：系统创建/资源准备
        Initialization, // ⚡ 新增：初始化阶段
        // 主菜单阶段：不进入游戏逻辑
        MainMenu,       // ⚡ 新增：主菜单
        // 自由探索阶段（非棋盘）
        FreeRoam,       
        // 棋盘模式阶段
        BoardMode,      
        // 对话/剧情阶段
        Dialogue,       
        // 暂停阶段
        Paused          
    }

    /// <summary>
    /// 场景模式（偏场景类型）：
    /// 由 SceneRegistrySO 定义，用于 SceneLoader 判断加载策略。
    /// </summary>
    public enum GameMode
    {
        // 菜单场景
        Menu,
        // 棋盘场景
        Board,
        // 探索场景
        Exploration
    }
}
