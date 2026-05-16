using UnityEngine;

namespace IndieGame.Gameplay.Treasure
{
    [CreateAssetMenu(menuName = "BoardGame/Treasure/Cloak Treasure", fileName = "CloakTreasureSO")]
    public class CloakTreasureSO : TreasureSO
    {
        // 斗篷宝具无额外专属字段
        // ActionPointCost 继承自基类，在 Inspector 中配置
    }
}
