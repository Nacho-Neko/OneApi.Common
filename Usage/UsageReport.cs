namespace OneApi.Common.Usage;

/// <summary>
/// 上游报的用量，<typeparamref name="T"/> 是那种用量（LLM 的输入 <see cref="LlmInput"/>、产出 <see cref="LlmOutput"/>……）。
/// 上游怎么报分两种，读取器按协议选：
/// <list type="bullet">
///   <item><see cref="Merge"/>：分次上报（Anthropic 先报输入、后报产出）。每一项取最后一次报的值，
///     这一次没报的项保留之前的——见 <see cref="IUsageUpdate{T}"/>。</item>
///   <item><see cref="Replace"/>：每份都是整份快照（Gemini、OpenAI），后一份整份取代前一份。</item>
/// </list>
/// </summary>
public sealed class UsageReport<T> where T : class
{
    /// <summary>上游到目前为止报的；null = 还没报。</summary>
    public T? Current { get; private set; }

    public void Merge(IUsageUpdate<T> update) => Current = update.Over(Current);

    /// <summary>
    /// 整份取代，快照里没有这一项就回到「没报」。快照协议的净输入不单调——
    /// 缓存数末帧才到，前几帧扣不了缓存，净输入要从毛值掉到真值。
    /// </summary>
    public void Replace(T? snapshot) => Current = snapshot;
}
