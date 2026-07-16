namespace OneApi.Common.Llm;

/// <summary>
/// 调用方协议格式常量。派发层（Demux.Gateway <c>DispatchCommand.CallerFormat</c>）与
/// 各 Provider 的 <c>NativeFormat</c> 对齐；决定原生直通与 conversationId 提取方式。
/// </summary>
public static class CallerFormats
{
    public const string OpenAiChat = "openai.chat";
    public const string OpenAiResponses = "openai.responses";
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";

    /// <summary>是否为已知格式（未知格式仍可派发，但不做原生直通优化）。</summary>
    public static bool IsKnown(string? format) =>
        format is OpenAiChat or OpenAiResponses or Anthropic or Gemini;

    /// <summary>归一：trim + 小写；空抛 <see cref="ArgumentException"/>。</summary>
    public static string NormalizeRequired(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("caller_format is required.", nameof(raw));
        return raw.Trim().ToLowerInvariant();
    }
}
