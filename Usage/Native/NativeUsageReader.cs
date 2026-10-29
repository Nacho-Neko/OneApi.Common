using System.Text.Json;

namespace OneApi.Common.Usage.Native;

/// <summary>
/// 从**上游原生** JSON 里读出用量的一套口径，一个协议一个实现。
///
/// <para><b>为什么值得单独抽出来。</b>四家的字段名不同只是表层，真正难的是每家
/// 净/毛的约定都不一样：OpenAI 的 <c>prompt_tokens</c> 含 cached 要减掉、Anthropic 的
/// <c>input_tokens</c> 本来就不含要原样过、Gemini 的 <c>candidatesTokenCount</c> 不含思考却含图像 / 音频。
/// 读取器在读的时候就换算成互不重叠的 <see cref="LlmInput"/> / <see cref="LlmOutput"/>，因为计价是按项<b>相加</b>的，
/// 换算错不会报错，只会重复计费或漏计费。一家一个类，至少让每家的口径在同一屏里看得全。</para>
///
/// <para>调用入口是 <see cref="NativeUsageParser"/>，它负责 SSE 拆帧和异常隔离。</para>
/// </summary>
public abstract class NativeUsageReader
{
    /// <summary>这个实现读哪一种原生格式。</summary>
    public abstract NativeUsageFormat Format { get; }

    /// <summary>
    /// 从一份原生响应（整包 JSON，或流式的一帧）里读出用量写进 <paramref name="reading"/>。
    /// 帧里没有用量是常态，直接返回即可。
    /// </summary>
    public abstract void Read(ReadOnlySpan<byte> utf8, LlmUsageReading reading);
}

/// <summary>
/// 把一帧反序列化成这家协议的帧实体再读。实体只声明用量和终止原因相关的字段，其余字段反序列化时跳过。
///
/// <para><b>计数一律声明成可空。</b>上游报了 0 是权威结论（内容被安全策略拦下就是
/// 这样：usage 齐全，<c>output_tokens</c> 就是 0），而字段压根没出现意味着这一帧
/// 还没到该报的时候——Gemini 在模型开口前不带 <c>candidatesTokenCount</c>。两者
/// 混成 0，消费方就分不清「官方说 0」和「官方没说」：前者该照扣 0，后者该按已吐出的文本补算。
/// 只有「0 和没有」等价的项（缓存 / 思考 / 各模态）才在读的时候 <c>?? 0</c>。</para>
/// </summary>
/// <param name="naming">这家协议的字段命名约定，实体属性名按它映射到 JSON 键。</param>
public abstract class NativeUsageReader<TFrame>(JsonNamingPolicy naming) : NativeUsageReader
    where TFrame : class
{
    private readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = naming };

    public sealed override void Read(ReadOnlySpan<byte> utf8, LlmUsageReading reading)
    {
        // 只认对象帧：Gemini 不带 alt=sse 的流整体是一个 JSON 数组，那种包里没有可单独计的一帧。
        var json = new Utf8JsonReader(utf8);
        if (!json.Read() || json.TokenType != JsonTokenType.StartObject)
            return;

        if (JsonSerializer.Deserialize<TFrame>(ref json, _options) is { } frame)
            Read(frame, reading);
    }

    /// <inheritdoc cref="NativeUsageReader.Read"/>
    protected abstract void Read(TFrame frame, LlmUsageReading reading);
}

/// <summary>
/// 格式 → 读取器。**唯一一处按格式分叉的地方**，加一种原生格式只改这里和它自己的子类。
/// </summary>
public static class NativeUsageReaders
{
    private static readonly NativeUsageReader OpenAiChat = new OpenAiChatUsageReader();
    private static readonly NativeUsageReader OpenAiResponses = new OpenAiResponsesUsageReader();
    private static readonly NativeUsageReader Anthropic = new AnthropicUsageReader();
    private static readonly NativeUsageReader Gemini = new GeminiUsageReader();

    /// <summary>
    /// <c>null</c> = 不认识的格式。**不抛**：读不出用量已经够糟，再把整条响应打断
    /// 就是把"账单少算"升级成"请求失败"。
    /// </summary>
    public static NativeUsageReader? For(NativeUsageFormat format) => format switch
    {
        NativeUsageFormat.OpenAiChat => OpenAiChat,
        NativeUsageFormat.OpenAiResponses => OpenAiResponses,
        NativeUsageFormat.Anthropic => Anthropic,
        NativeUsageFormat.Gemini => Gemini,
        _ => null,
    };
}
