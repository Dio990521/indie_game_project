namespace IndieGame.Core.Utilities
{
    /// <summary>
    /// 表示一个带有随机抽取权重的条目。
    /// 实现此接口后可直接传入 WeightedRandomUtil.Pick 进行加权随机。
    /// </summary>
    public interface IWeighted
    {
        /// <summary>权重值，必须大于 0 才会被纳入抽取池。</summary>
        int Weight { get; }
    }
}
