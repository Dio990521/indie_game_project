using UnityEngine;

namespace IndieGame.Gameplay.Treasure
{
    /// <summary>
    /// 反转牌宝具配置：使用后玩家原地跳跃并旋转180°，反转行进方向。
    /// 下次掷骰子时将朝反方向行进。
    /// 在 Inspector 中设置 TreasureId = "reverse_card"，ActionPointCost = 1。
    /// </summary>
    [CreateAssetMenu(menuName = "BoardGame/Treasure/Reverse Card Treasure", fileName = "ReverseCardTreasureSO")]
    public class ReverseCardTreasureSO : TreasureSO
    {
        [Header("反转动画")]
        [Tooltip("跳跃 + 旋转同步播放的总时长（秒），推荐 0.5")]
        public float AnimationDuration = 0.5f;

        [Tooltip("原地跳跃的峰值高度（世界空间单位），推荐 1.5")]
        public float JumpHeight = 1.5f;
    }
}
