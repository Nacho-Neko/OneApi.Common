using OneApi.Common.Providers.Streaming;

namespace OneApi.Common.Usage;

/// <summary>
/// 一次 LLM 调用里上游报的用量和终止原因。原生响应由 <c>NativeUsageReader</c> 按协议读进来，
/// OneApi 规范用量（<see cref="ProviderUsageKeys"/> 那个字典）由 <see cref="ObserveCanonical"/> 读进来。
/// <para>
/// 输入、产出各是一份 <see cref="UsageReport{T}"/>，因为上游就是分开报的——Anthropic 先报输入、后报产出，
/// 流被掐在终端 usage 之前时缺的正好是产出那一半。哪一半没报就是 null，拿什么兜底由消费方定
/// （Yomi 用请求体数出的输入、已吐出文本数出的产出）。上游报了 0 就是 0，不是没报。
/// </para>
/// </summary>
public sealed class LlmUsageReading
{
    /// <summary>上游报的输入。读取器按协议选合并（分次上报）或取代（整份快照）。</summary>
    public UsageReport<LlmInput> Input { get; } = new();

    /// <summary>上游报的产出。同上。</summary>
    public UsageReport<LlmOutput> Output { get; } = new();

    /// <summary>返回给调用方的终止原因原文（按协议，不归一），最后报的为准；没读到是 null。</summary>
    public string? StopReason { get; private set; }

    /// <summary>空串不算：上游中间帧常带一个空的 finish_reason 占位。</summary>
    public void ObserveStopReason(string? reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
            StopReason = reason.Trim();
    }

    /// <summary>
    /// OneApi 线上的规范用量字典。累计口径，分次报，所以是合并。
    /// 输出键是毛产出（含思考和模态），在这里换算成互不重叠的净值；积分、总数之类的键不属于 LLM 用量，忽略。
    /// </summary>
    public void ObserveCanonical(IReadOnlyDictionary<string, long> usage)
    {
        long? Read(string key) => usage.TryGetValue(key, out var value) ? value : null;

        var prompt = Read(ProviderUsageKeys.InputTokens);
        var cachedRead = Read(ProviderUsageKeys.CacheReadInputTokens);
        var cachedWrite = Read(ProviderUsageKeys.CacheCreationInputTokens);
        if (prompt is not null || cachedRead is not null || cachedWrite is not null)
            Input.Merge(new LlmInput { Prompt = prompt ?? 0, CachedRead = cachedRead ?? 0, CachedWrite = cachedWrite ?? 0 });

        if (Read(ProviderUsageKeys.OutputTokens) is { } gross)
        {
            var reasoning = Read(ProviderUsageKeys.ReasoningTokens) ?? 0;
            var image = Read(ProviderUsageKeys.OutputImageTokens) ?? 0;
            var audio = Read(ProviderUsageKeys.OutputAudioTokens) ?? 0;
            Output.Merge(new LlmOutput
            {
                Text = Math.Max(0, gross - reasoning - image - audio),
                Reasoning = reasoning,
                Image = image,
                Audio = audio,
            });
        }
    }
}
