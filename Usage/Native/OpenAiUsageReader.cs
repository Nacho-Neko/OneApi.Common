using System.Text.Json;

namespace OneApi.Common.Usage.Native;

/// <summary>
/// OpenAI 两个 API 的共同口径。净/毛的约定完全一致——输入是毛的（含 cached）、输出是毛的
/// （含 reasoning）——差的只有字段拼写和 usage 块挂在哪。所以换算写一次，两边的 usage 实体
/// 各自按自己的拼写声明，再经 <see cref="IOpenAiUsage"/> 交出同一组数。
/// </summary>
public abstract class OpenAiUsageReader<TFrame>() : NativeUsageReader<TFrame>(JsonNamingPolicy.SnakeCaseLower)
    where TFrame : class
{
    /// <summary>
    /// 输入是毛的：明细块是它的拆分，cached_tokens（缓存读）和 Responses 上的 cache_write_tokens（缓存写）都在里面，
    /// 减掉才是净输入，两者各按自己的单价收（写缓存和读缓存价格不同）。
    /// 产出也是毛的：reasoning_tokens / audio_tokens 都在 completion 里，剥出来单列，正文是剩下的。
    /// </summary>
    protected static void ApplyUsage(IOpenAiUsage usage, LlmUsageReading reading)
    {
        // 每个 usage 对象都是整份累计用量：官方只在末个 chunk 带，baseten 等 vLLM 系上游每个 chunk 都带一份。
        // 早到的那几份可能不完整（缓存数后到），所以后一份整份取代前一份。
        var cached = usage.InputDetails?.CachedTokens ?? 0;
        var written = usage.InputDetails?.CacheWriteTokens ?? 0;
        reading.Input.Replace(usage.Input is not null || cached > 0 || written > 0
            ? new LlmInput { Prompt = Math.Max(0, (usage.Input ?? 0) - cached - written), CachedRead = cached, CachedWrite = written }
            : null);

        var reasoning = usage.OutputDetails?.ReasoningTokens ?? 0;
        var audio = usage.OutputDetails?.AudioTokens ?? 0;
        reading.Output.Replace(usage.Output is { } completion
            ? new LlmOutput { Text = Math.Max(0, completion - reasoning - audio), Reasoning = reasoning, Audio = audio }
            : null);
    }
}

/// <summary>OpenAI Chat Completions：usage 就挂在根上。</summary>
public sealed class OpenAiChatUsageReader : OpenAiUsageReader<OpenAiChatFrame>
{
    public override NativeUsageFormat Format => NativeUsageFormat.OpenAiChat;

    protected override void Read(OpenAiChatFrame frame, LlmUsageReading reading)
    {
        reading.ObserveStopReason(frame.Choices?.FirstOrDefault()?.FinishReason);

        if (frame.Usage is { } usage)
            ApplyUsage(usage, reading);
    }
}

/// <summary>
/// OpenAI Responses：非流式回包本身就是响应对象，usage 在根上；事件流的响应对象包在 <c>response</c> 里，
/// usage 只在完成帧上。
/// </summary>
public sealed class OpenAiResponsesUsageReader : OpenAiUsageReader<OpenAiResponsesFrame>
{
    public override NativeUsageFormat Format => NativeUsageFormat.OpenAiResponses;

    protected override void Read(OpenAiResponsesFrame frame, LlmUsageReading reading)
    {
        var response = frame.Response ?? frame;
        reading.ObserveStopReason(response.StopReason());

        if ((response.Usage ?? frame.Usage) is { } usage)
            ApplyUsage(usage, reading);
    }
}

/// <summary>两个 API 的 usage 拼写不同、含义相同，换算只认这一组。</summary>
public interface IOpenAiUsage
{
    /// <summary>毛输入，含 <see cref="OpenAiInputDetails"/> 里的缓存读写。</summary>
    long? Input { get; }

    /// <summary>毛产出，含 <see cref="OpenAiOutputDetails"/> 里的推理和音频。</summary>
    long? Output { get; }

    OpenAiInputDetails? InputDetails { get; }
    OpenAiOutputDetails? OutputDetails { get; }
}

public sealed record OpenAiInputDetails
{
    public long? CachedTokens { get; init; }

    /// <summary>只有 Responses 报。</summary>
    public long? CacheWriteTokens { get; init; }
}

public sealed record OpenAiOutputDetails
{
    public long? ReasoningTokens { get; init; }
    public long? AudioTokens { get; init; }
}

/// <summary>Chat Completions 的一帧（chunk 或整包），只取用量和终止原因。</summary>
public sealed record OpenAiChatFrame
{
    public OpenAiChatChoice?[]? Choices { get; init; }
    public OpenAiChatUsage? Usage { get; init; }
}

public sealed record OpenAiChatChoice
{
    /// <summary>中间帧是 null，只有末帧带 stop / length / tool_calls / content_filter。</summary>
    public string? FinishReason { get; init; }
}

public sealed record OpenAiChatUsage : IOpenAiUsage
{
    public long? PromptTokens { get; init; }
    public long? CompletionTokens { get; init; }
    public OpenAiInputDetails? PromptTokensDetails { get; init; }
    public OpenAiOutputDetails? CompletionTokensDetails { get; init; }

    long? IOpenAiUsage.Input => PromptTokens;
    long? IOpenAiUsage.Output => CompletionTokens;
    OpenAiInputDetails? IOpenAiUsage.InputDetails => PromptTokensDetails;
    OpenAiOutputDetails? IOpenAiUsage.OutputDetails => CompletionTokensDetails;
}

/// <summary>Responses 的响应对象：非流式回包本身，或事件流里 <c>response</c> 包着的那个。</summary>
public record OpenAiResponse
{
    public string? Status { get; init; }
    public OpenAiIncompleteDetails? IncompleteDetails { get; init; }
    public OpenAiResponsesUsage? Usage { get; init; }

    /// <summary>
    /// Responses 没有 finish_reason：截断时 status 是 incomplete，原因在 incomplete_details.reason
    /// （max_output_tokens / content_filter），优先用它；否则只认终态的 status，in_progress 之类的中间态不算。
    /// </summary>
    public string? StopReason()
        => IncompleteDetails?.Reason is { Length: > 0 } reason ? reason
            : Status is "completed" or "incomplete" or "failed" or "cancelled" ? Status
            : null;
}

/// <summary>Responses 的一帧：事件（带 <c>type</c> 和 <c>response</c>），或非流式回包（它自己就是响应对象）。</summary>
public sealed record OpenAiResponsesFrame : OpenAiResponse
{
    public OpenAiResponse? Response { get; init; }
}

public sealed record OpenAiIncompleteDetails
{
    public string? Reason { get; init; }
}

public sealed record OpenAiResponsesUsage : IOpenAiUsage
{
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public OpenAiInputDetails? InputTokensDetails { get; init; }
    public OpenAiOutputDetails? OutputTokensDetails { get; init; }

    long? IOpenAiUsage.Input => InputTokens;
    long? IOpenAiUsage.Output => OutputTokens;
    OpenAiInputDetails? IOpenAiUsage.InputDetails => InputTokensDetails;
    OpenAiOutputDetails? IOpenAiUsage.OutputDetails => OutputTokensDetails;
}
