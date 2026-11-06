using System.Text.Json.Serialization;

namespace OneApi.Common.Providers.Streaming;

/// <summary>
/// Standard streaming event emitted by any provider toward the gateway worker mesh.
/// </summary>
public class ProviderEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("thinkingContent")]
    public string? ThinkingContent { get; set; }

    [JsonPropertyName("isFirstThinkingChunk")]
    public bool IsFirstThinkingChunk { get; set; }

    [JsonPropertyName("isLastThinkingChunk")]
    public bool IsLastThinkingChunk { get; set; }

    /// <summary>
    /// Opaque provider signature for the block this event carries — an Anthropic
    /// <c>thinking.signature</c> or a Gemini <c>thoughtSignature</c>. Set on
    /// <c>thinking</c> events and, for Gemini 3, on <c>tool_start</c>: the signature
    /// rides on the <c>functionCall</c> part and must be replayed there next turn.
    /// </summary>
    [JsonPropertyName("thinkingSignature")]
    public string? ThinkingSignature { get; set; }

    [JsonPropertyName("toolName")]
    public string? ToolName { get; set; }

    [JsonPropertyName("toolUseId")]
    public string? ToolUseId { get; set; }

    [JsonPropertyName("usage")]
    public Dictionary<string, object>? Usage { get; set; }

    [JsonPropertyName("contextUsagePercentage")]
    public double? ContextUsagePercentage { get; set; }
}
