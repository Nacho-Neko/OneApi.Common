namespace OneApi.Common.Llm;

/// <summary>
/// 协议格式的 wire 字符串常量，只用于外部边界：Consul catalog 的
/// <c>defaultIngestFormat</c>、各 Provider 的 <c>NativeFormat</c>、HTTP 头。
/// 服务间契约请用 <see cref="WireFormat"/> 枚举，两者用
/// <see cref="WireFormats.TryParse"/> / <see cref="WireFormats.ToWireString"/> 互转。
/// </summary>
public static class CallerFormats
{
    public const string OpenAiChat = "openai.chat";
    public const string OpenAiResponses = "openai.responses";
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";
}
