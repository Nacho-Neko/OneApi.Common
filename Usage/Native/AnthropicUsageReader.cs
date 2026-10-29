using System.Text.Json;

namespace OneApi.Common.Usage.Native;

/// <summary>
/// Anthropic。<b>输入是净的</b>——两项缓存与 <c>input_tokens</c> 并列上报、不含在里面——
/// 所以这里不像 OpenAI/Gemini 那样要减 cached，减了就是把缓存命中重复扣一遍。
/// </summary>
public sealed class AnthropicUsageReader() : NativeUsageReader<AnthropicFrame>(JsonNamingPolicy.SnakeCaseLower)
{
    public override NativeUsageFormat Format => NativeUsageFormat.Anthropic;

    protected override void Read(AnthropicFrame frame, LlmUsageReading reading)
    {
        reading.ObserveStopReason(frame.Delta?.StopReason ?? frame.StopReason);

        // Anthropic 流式响应 usage 拆成两处：message_start 的 input + cache 在 message.usage（嵌套）；
        // message_delta 的最终 output 在根级 usage。两者都要观测，否则透传计费的 input 永远为 0。
        // 非流式 / 单体 JSON 的 usage 同样在根级。
        var (usage, interim) = frame.Message?.Usage is { } start ? (start, true) : (frame.Usage, false);
        if (usage is null)
            return;

        // Anthropic 分两次报（输入在 message_start、产出在 message_delta），所以两半都是合并而不是取代。
        //
        // input_tokens 已经是净的：cache_read 和 cache_creation 与它并列上报、不含在里面，原样用。
        // 缓存读和缓存写是两份不同的输入，不是同一个数的两个名字：读 25944 同时写 7233 就是花了 33177，
        // 两者取最大会只收大的那份、把另一份悄悄丢掉。
        var cachedRead = usage.CacheReadInputTokens ?? 0;
        var cachedWrite = usage.CacheCreationInputTokens ?? 0;
        if (usage.InputTokens is not null || cachedRead > 0 || cachedWrite > 0)
            reading.Input.Merge(new LlmInput { Prompt = usage.InputTokens ?? 0, CachedRead = cachedRead, CachedWrite = cachedWrite });

        // Anthropic 的 usage 是累计口径，message_start 报的是「发这一帧时已经产出多少」——
        // 一个偏小、但不固定的部分量，终值只在 message_delta 的根级 usage 上。
        // OneApi 的三条真实抓包（Anthropic SDK 测试里的 Helpers/FableFallback/*.sse）：
        // message_start 是 5 / 7 / 63，对应终值 106 / 547 / 106。
        //
        // 所以它不能当官方产出记下来：客户端掐流后计量器会以为官方已经报过产出，
        // 于是按这个部分量出账、而不是按已经吐给用户的那一大段文本。上面那三条就是
        // 少扣 40% ~ 99%。完整流不受影响，终值照样从 message_delta 进来。
        if (interim || usage.OutputTokens is not { } output)
            return;

        // output_tokens 是毛的，含 thinking：剥出来单列，正文是剩下的那部分。
        var thinking = usage.OutputTokensDetails?.ThinkingTokens ?? 0;
        reading.Output.Merge(new LlmOutput { Text = Math.Max(0, output - thinking), Reasoning = thinking });
    }
}

/// <summary>
/// Anthropic 的一帧：流式的 <c>message_start</c> / <c>message_delta</c>，或非流式的整个 message。只取用量和终止原因。
/// </summary>
public sealed record AnthropicFrame
{
    /// <summary><c>message_start</c> 里的 message，输入和缓存在它的 usage 上；它的 stop_reason 恒为 null，不读。</summary>
    public AnthropicStartMessage? Message { get; init; }

    /// <summary><c>message_delta</c> 的终止原因在这里。</summary>
    public AnthropicMessageDelta? Delta { get; init; }

    /// <summary>非流式回包的终止原因在根上。</summary>
    public string? StopReason { get; init; }

    /// <summary><c>message_delta</c> 和非流式回包的 usage 在根上。</summary>
    public AnthropicUsage? Usage { get; init; }
}

public sealed record AnthropicStartMessage
{
    public AnthropicUsage? Usage { get; init; }
}

public sealed record AnthropicMessageDelta
{
    public string? StopReason { get; init; }
}

public sealed record AnthropicUsage
{
    /// <summary>净输入，不含两项缓存。</summary>
    public long? InputTokens { get; init; }

    public long? CacheReadInputTokens { get; init; }
    public long? CacheCreationInputTokens { get; init; }

    /// <summary>毛产出，含 <see cref="AnthropicOutputDetails.ThinkingTokens"/>。</summary>
    public long? OutputTokens { get; init; }

    public AnthropicOutputDetails? OutputTokensDetails { get; init; }
}

public sealed record AnthropicOutputDetails
{
    public long? ThinkingTokens { get; init; }
}
