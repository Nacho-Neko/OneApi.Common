namespace OneApi.Common.Usage;

/// <summary>
/// 一种用量自己声明：同一次调用里上游分次报了两份，后到的一份怎么并进已有的。
/// 读取器按协议决定是合并（分次上报）还是整份取代（快照上报），合并规则则由用量自己定。
/// </summary>
public interface IMergeableUsage<T> where T : class, IMergeableUsage<T>
{
    T Merge(T later);
}

/// <summary>
/// 上游报的用量，<typeparamref name="T"/> 是那种用量（LLM 的输入 <see cref="LlmInput"/>、产出 <see cref="LlmOutput"/>……）。
/// 上游怎么报分两种，读取器按协议选：
/// <list type="bullet">
///   <item><see cref="Merge"/>：分次上报（Anthropic 先报输入、后报产出），合并规则由 <typeparamref name="T"/> 自己定。</item>
///   <item><see cref="Replace"/>：每份都是整份快照（Gemini、OpenAI），后一份整份取代前一份。</item>
/// </list>
/// </summary>
public sealed class UsageReport<T> where T : class, IMergeableUsage<T>
{
    /// <summary>上游到目前为止报的；null = 还没报。</summary>
    public T? Current { get; private set; }

    public void Merge(T report) => Current = Current is null ? report : Current.Merge(report);

    /// <summary>
    /// 整份取代，快照里没有这一项就回到「没报」。逐项合并只对单调的量成立，快照协议的净输入不单调——
    /// 缓存数末帧才到，前几帧扣不了缓存，净输入要从毛值掉到真值，合并就把毛值留下了。
    /// </summary>
    public void Replace(T? snapshot) => Current = snapshot;
}
