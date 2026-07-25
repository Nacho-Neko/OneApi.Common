namespace OneApi.Common.Llm;

/// <summary>
/// 协议格式的规范表示。自家服务之间的契约（<c>DispatchCommand</c>、<c>TaskEnvelope</c>
/// 及之后的派发链路）一律用本枚举，不传自由字符串。字符串形式
/// （<see cref="CallerFormats"/>）只留给取值域本就超出这四种的外部边界：HTTP 头、
/// Consul catalog JSON、compat 账号自填的 protocol、provider 私有的 NativeFormat
/// （<c>"cursor"</c> / <c>"kiro"</c> 之类）。边界解析用 <see cref="WireFormats.TryParse"/>：
/// 未知字符串在边界就地拒绝或降级，内部不可能出现未定义值。
/// </summary>
public enum WireFormat : byte
{
    /// <summary>未声明（旧客户端未填 / 内部调用方不关心）。派发仍可进行，但严格转码会拒绝无格式的跨协议请求。</summary>
    Unspecified = 0,

    /// <summary>OpenAI Chat Completions（"openai.chat"）。</summary>
    OpenAiChat = 1,

    /// <summary>OpenAI Responses API（"openai.responses"）。</summary>
    OpenAiResponses = 2,

    /// <summary>Anthropic Messages（"anthropic"）。</summary>
    Anthropic = 3,

    /// <summary>Google Gemini generateContent（"gemini"）。</summary>
    Gemini = 4,
}

/// <summary><see cref="WireFormat"/> 与边界字符串（<see cref="CallerFormats"/>）的互转。</summary>
public static class WireFormats
{
    /// <summary>
    /// 边界解析：null/空白 → <see cref="WireFormat.Unspecified"/>；已知格式 → 对应枚举；
    /// 未知非空字符串 → <c>null</c>（调用方应拒绝并回 400，绝不静默降级）。
    /// 接受 "openai"/"openai-chat" 等历史别名，与 ChatTransit 的解析口径一致。
    /// </summary>
    public static WireFormat? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return WireFormat.Unspecified;

        return raw.Trim().ToLowerInvariant() switch
        {
            "openai.chat" or "openai-chat" or "openai" => WireFormat.OpenAiChat,
            "openai.responses" or "openai-responses" => WireFormat.OpenAiResponses,
            "anthropic" or "claude" => WireFormat.Anthropic,
            "gemini" or "google" => WireFormat.Gemini,
            _ => null,
        };
    }

    /// <summary>
    /// 边界输出：枚举 → 规范字符串（<see cref="CallerFormats"/> 取值域）。
    /// <see cref="WireFormat.Unspecified"/> → 空串。
    /// </summary>
    public static string ToWireString(this WireFormat format) => format switch
    {
        WireFormat.OpenAiChat => CallerFormats.OpenAiChat,
        WireFormat.OpenAiResponses => CallerFormats.OpenAiResponses,
        WireFormat.Anthropic => CallerFormats.Anthropic,
        WireFormat.Gemini => CallerFormats.Gemini,
        _ => string.Empty,
    };

    /// <summary>
    /// 判断调用方格式是否与某个 <c>NativeFormat</c> 是同一协议。NativeFormat 留在字符串域
    /// （provider 私有值 <c>"cursor"</c> / <c>"kiro"</c> 不在本枚举取值域内），所以这里是
    /// 枚举与字符串唯一该相遇的地方——其余判定一律直接比枚举。
    /// <see cref="WireFormat.Unspecified"/> 不匹配任何格式（调用方没声明协议就不能直通）。
    /// </summary>
    public static bool Matches(this WireFormat format, string? nativeFormat)
        => format != WireFormat.Unspecified
           && string.Equals(format.ToWireString(), nativeFormat, StringComparison.OrdinalIgnoreCase);
}
