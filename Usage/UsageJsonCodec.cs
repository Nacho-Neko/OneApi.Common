using System.Text.Json;

namespace OneApi.Common.Usage;

/// <summary>
/// usage json 编解码的唯一实现。Demux 的 <c>usage_logs.usage</c> 与
/// Tavern 的 <c>chat_message_usages.usage_json</c> 都经此序列化，保证两边形状永远一致。
/// </summary>
public static class UsageJsonCodec
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    // ── token 形（chat / wake / embedding / tool）──────────────────────────

    public static string Serialize(TokenUsage usage) =>
        JsonSerializer.Serialize(BuildDocument(usage), JsonOpts);

    public static TokenUsageDocument BuildDocument(TokenUsage usage)
    {
        var input = Math.Max(0, usage.InputTokens);
        var output = Math.Max(0, usage.OutputTokens);
        return new TokenUsageDocument
        {
            TotalTokens = input + output,
            Input = new TokenUsageInput
            {
                Tokens = input,
                CachedReadTokens = Math.Max(0, usage.CachedReadTokens),
                CachedWriteTokens = Math.Max(0, usage.CachedWriteTokens),
                AudioTokens = Math.Max(0, usage.InputAudioTokens),
            },
            Output = new TokenUsageOutput
            {
                Tokens = output,
                ReasoningTokens = Math.Max(0, usage.ReasoningTokens),
                AudioTokens = Math.Max(0, usage.OutputAudioTokens),
            },
        };
    }

    /// <summary>宽松解析：空 / 非法 JSON 返回全零文档，绝不抛错拖垮读路径。</summary>
    public static TokenUsageDocument Parse(string? usageJson)
    {
        if (string.IsNullOrWhiteSpace(usageJson))
            return new TokenUsageDocument();
        try
        {
            return JsonSerializer.Deserialize<TokenUsageDocument>(usageJson, JsonOpts)
                   ?? new TokenUsageDocument();
        }
        catch (JsonException)
        {
            return new TokenUsageDocument();
        }
    }

    public static TokenUsage ToTokenUsage(string? usageJson)
    {
        var doc = Parse(usageJson);
        return new TokenUsage(
            doc.Input.Tokens,
            doc.Output.Tokens,
            doc.Output.ReasoningTokens,
            doc.Input.CachedReadTokens,
            doc.Input.CachedWriteTokens,
            doc.Input.AudioTokens,
            doc.Output.AudioTokens);
    }

    // ── 媒体形（image / audio / video）────────────────────────────────────

    public static string SerializeImage(int count, string? size = null, string? quality = null) =>
        JsonSerializer.Serialize(new ImageUsageDocument
        {
            Tier = new ImageUsageTier { Size = size ?? "unknown", Quality = quality ?? "default" },
            Count = Math.Max(0, count),
        }, JsonOpts);

    public static string SerializeAudio(decimal seconds) =>
        JsonSerializer.Serialize(new AudioUsageDocument { Seconds = Math.Max(0, seconds) }, JsonOpts);

    public static string SerializeVideo(decimal seconds, string? resolution = null) =>
        JsonSerializer.Serialize(new VideoUsageDocument
        {
            Tier = new VideoUsageTier { Resolution = resolution ?? "unknown" },
            Seconds = Math.Max(0, seconds),
        }, JsonOpts);
}
