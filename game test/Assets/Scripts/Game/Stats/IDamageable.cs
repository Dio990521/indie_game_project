namespace IndieGame.Gameplay.Stats
{
    /// <summary>
    /// 可受伤接口：
    /// 任何实现该接口的对象都应响应伤害输入，方便战斗系统统一调用。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 受到伤害。
        /// </summary>
        /// <param name="amount">伤害数值（通常为正数）</param>
        void TakeDamage(int amount);
    }
}
