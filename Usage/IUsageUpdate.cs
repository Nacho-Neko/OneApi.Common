namespace OneApi.Common.Usage;

/// <summary>
/// 分次上报里的一次：只带这一次真正读到的项。并进已有的时，报了的项一律以这一次为准，没报的项保留之前的。
/// <para>
/// 不取最大：上游后报的可能比先报的小——Kiro 调上游前先报一个整段输入的估计值，结束时才报拆出缓存后的净输入——
/// 取最大会留下那个估计值，再加上缓存读写，同一批输入收两遍。上游的累计口径本来就是后报覆盖先报。
/// </para>
/// <para>
/// 「没报」和「报了 0」要分开：Anthropic 的 <c>message_delta</c> 只带产出时，<c>message_start</c> 报的缓存读不能被 0 盖掉；
/// 上游明确报了 0 就是 0。所以这里的项是可空的，不能沿用 <typeparamref name="T"/> 本身（它的项不可空，是跨服务契约）。
/// </para>
/// </summary>
public interface IUsageUpdate<T> where T : class
{
    /// <summary>把这一次盖到 <paramref name="earlier"/> 上；<paramref name="earlier"/> 为 null 表示之前没报过，没报的项记 0。</summary>
    T Over(T? earlier);
}
