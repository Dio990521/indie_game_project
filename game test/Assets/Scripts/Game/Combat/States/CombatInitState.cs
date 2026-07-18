using IndieGame.Core.Utilities;

namespace IndieGame.Gameplay.Combat.States
{
    /// <summary>
    /// 战斗初始化状态：
    /// 消费 CombatLaunchContext 载荷（构建名册、生成主角与第 0 波敌人、绑定战斗相机），
    /// 完成后广播 CombatStartedEvent 并进入 CombatActiveState。
    /// 具体生成逻辑收敛在 CombatManager.SetupBattle（状态只做流程编排）。
    /// </summary>
    public class CombatInitState : CombatState
    {
        public override void OnEnter(CombatManager context)
        {
            DebugTools.Log("<color=orange>[Combat] 战斗初始化…</color>");

            if (!context.SetupBattle())
            {
                DebugTools.LogError("[Combat] 战斗初始化失败（缺少遭遇配置或场景引用），停留在 Init 状态。");
                return;
            }

            context.ChangeState(new CombatActiveState());
        }
    }
}
