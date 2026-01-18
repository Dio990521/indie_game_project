namespace IndieGame.Core
{
    public enum GameState
    {
        Initialization, // ⚡ 新增：初始化阶段
        MainMenu,       // ⚡ 新增：主菜单
        FreeRoam,       
        BoardMode,      
        Dialogue,       
        Paused          
    }
}
