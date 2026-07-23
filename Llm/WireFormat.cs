namespace OneApi.Common.Llm;

/// <summary>
/// 协议格式的内部枚举表示。字符串形式（<see cref="CallerFormats"/>）只存在于
/// 跨进程/跨仓边界（HTTP 头、<c>DispatchCommand</c>、Consul catalog JSON）；
/// 一旦进入网关内部（<c>TaskEnvelope</c> 及之后的派发链路），一律用本枚举，
/// 不再传自由字符串。边界解析用 <see cref="WireFormats.TryParse"/>：未知字符串
/// 在边界就地拒绝（400），内部不可能出现未定义值。
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
}
