namespace OneApi.Common.Usage;

/// <summary>
/// 一次 token 计量调用的原始计数（流式链路 / 上游响应累计），
/// 写库前由 <see cref="UsageJsonCodec"/> 折叠为标准 usage json。
/// 与 DemuxAi 的 <c>TokenUsageBreakdown</c>（wire 契约）字段语义一一对应：
/// Prompt→Input、Completion→Output、Cached→CachedRead、Audio→InputAudio。
/// </summary>
public sealed record TokenUsage(
    int InputTokens = 0,
    int OutputTokens = 0,
    int ReasoningTokens = 0,
    int CachedReadTokens = 0,
    int CachedWriteTokens = 0,
    int InputAudioTokens = 0,
    int OutputAudioTokens = 0)
{
    /// <summary>是否有任何可记录的计数（全 0 的调用可跳过落库）。</summary>
    public bool HasAny =>
        InputTokens > 0 || OutputTokens > 0 || ReasoningTokens > 0
        || CachedReadTokens > 0 || CachedWriteTokens > 0
        || InputAudioTokens > 0 || OutputAudioTokens > 0;
}
