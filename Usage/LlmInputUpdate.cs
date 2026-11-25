namespace OneApi.Common.Usage;

/// <summary>
/// 一次上报读到的 LLM 输入，各项与 <see cref="LlmInput"/> 同义；null = 这一次没报这一项。
/// </summary>
public sealed record LlmInputUpdate(
    long? Prompt = null,
    long? CachedRead = null,
    long? CachedWrite = null,
    long? CachedWrite1h = null) : IUsageUpdate<LlmInput>
{
    /// <summary>这一次一项都没带：读取器据此决定要不要并进去，免得一份空的上报把「没报」变成「报了 0」。</summary>
    public bool IsEmpty => Prompt is null && CachedRead is null && CachedWrite is null && CachedWrite1h is null;

    public LlmInput Over(LlmInput? earlier) => new()
    {
        Prompt        = Prompt        ?? earlier?.Prompt        ?? 0,
        CachedRead    = CachedRead    ?? earlier?.CachedRead    ?? 0,
        CachedWrite   = CachedWrite   ?? earlier?.CachedWrite   ?? 0,
        CachedWrite1h = CachedWrite1h ?? earlier?.CachedWrite1h ?? 0,
    };
}
