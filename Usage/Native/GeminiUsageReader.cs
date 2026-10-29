using System.Text.Json;

namespace OneApi.Common.Usage.Native;

/// <summary>
/// Gemini。输入是毛的（promptTokenCount 含 cachedContentTokenCount），要减掉缓存；
/// 产出里思考是单列的（candidatesTokenCount 不含 thoughtsTokenCount），图像 / 音频则是 candidates 的拆分。
/// 也是唯一一家单独上报图像 / 音频 / 视频产出 token 的。
/// </summary>
public sealed class GeminiUsageReader() : NativeUsageReader<GeminiFrame>(JsonNamingPolicy.CamelCase)
{
    public override NativeUsageFormat Format => NativeUsageFormat.Gemini;

    protected override void Read(GeminiFrame frame, LlmUsageReading reading)
    {
        reading.ObserveStopReason(frame.Candidates?.FirstOrDefault()?.FinishReason);

        // 一个计数都没有（只有 trafficType）就不是一次用量报告。
        if (frame.UsageMetadata is not { } meta
            || meta is { PromptTokenCount: null, CachedContentTokenCount: null, ThoughtsTokenCount: null, CandidatesTokenCount: null })
            return;

        // usageMetadata 每帧都是整份累计快照，但前面的帧不完整：缓存数只在末帧报。拿前面的帧算净输入就是
        // 没扣缓存的毛值，所以每份报告整份取代上一份，最后一份为准。
        // 末帧没写 cachedContentTokenCount 就是没命中缓存（proto3 JSON 不写零值）。
        var cached = meta.CachedContentTokenCount ?? 0;

        // 工具执行结果喂回模型的那份输入（toolUsePromptTokenCount）在 promptTokenCount 之外，要加上：
        // 官方 totalTokenCount = prompt + tool-use prompt + thoughts + candidates。
        reading.Input.Replace(meta.PromptTokenCount is not null || cached > 0
            ? new LlmInput
            {
                Prompt = Math.Max(0, (meta.PromptTokenCount ?? 0) - cached) + (meta.ToolUsePromptTokenCount ?? 0),
                CachedRead = cached,
            }
            : null);

        // 模型还没开口时没有 candidatesTokenCount，这时产出是「官方还没报」而不是「官方说 0」；
        // 只思考没出正文的那一帧则是真报了。
        // 图像 / 音频有自己的单价，从 candidates 里剥出来；视频没有，留在正文里按正文单价计。
        var image = meta.CandidateTokens("IMAGE");
        var audio = meta.CandidateTokens("AUDIO");
        reading.Output.Replace(meta.CandidatesTokenCount is not null || meta.ThoughtsTokenCount is not null
            ? new LlmOutput
            {
                Text = Math.Max(0, (meta.CandidatesTokenCount ?? 0) - image - audio),
                Reasoning = meta.ThoughtsTokenCount ?? 0,
                Image = image,
                Audio = audio,
            }
            : null);
    }
}

/// <summary>Gemini 的一帧（<c>GenerateContentResponse</c>），只取用量和终止原因。</summary>
public sealed record GeminiFrame
{
    public GeminiCandidate?[]? Candidates { get; init; }
    public GeminiUsageMetadata? UsageMetadata { get; init; }
}

public sealed record GeminiCandidate
{
    /// <summary>STOP / MAX_TOKENS / SAFETY ...，只有末帧带，不跟 usageMetadata 绑在一起。</summary>
    public string? FinishReason { get; init; }
}

public sealed record GeminiUsageMetadata
{
    /// <summary>毛输入，含 <see cref="CachedContentTokenCount"/>。</summary>
    public long? PromptTokenCount { get; init; }

    public long? CachedContentTokenCount { get; init; }

    /// <summary>工具执行结果喂回模型的输入，不在 <see cref="PromptTokenCount"/> 里。</summary>
    public long? ToolUsePromptTokenCount { get; init; }

    /// <summary>思考，不在 <see cref="CandidatesTokenCount"/> 里。</summary>
    public long? ThoughtsTokenCount { get; init; }

    /// <summary>产出，含各模态；<see cref="CandidatesTokensDetails"/> 是它的拆分。</summary>
    public long? CandidatesTokenCount { get; init; }

    public GeminiModalityTokenCount?[]? CandidatesTokensDetails { get; init; }

    /// <summary>某个模态（IMAGE / AUDIO / VIDEO）在产出里占的 token；没报就是 0。</summary>
    public long CandidateTokens(string modality)
        => CandidatesTokensDetails?
               .Where(d => string.Equals(d?.Modality, modality, StringComparison.OrdinalIgnoreCase))
               .Sum(d => d!.TokenCount ?? 0)
           ?? 0;
}

public sealed record GeminiModalityTokenCount
{
    public string? Modality { get; init; }
    public long? TokenCount { get; init; }
}
