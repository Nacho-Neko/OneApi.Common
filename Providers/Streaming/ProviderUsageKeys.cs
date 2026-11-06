using System.Text.Json;
using OneApi.Common.Usage;

namespace OneApi.Common.Providers.Streaming;

/// <summary>
/// Provider → Gateway stream usage dictionary keys (aligned with <see cref="UsageStreamKeys"/>).
/// </summary>
public static class ProviderUsageKeys
{
    public const string InputTokens = UsageStreamKeys.InputTokens;
    public const string OutputTokens = UsageStreamKeys.OutputTokens;
    public const string TotalTokens = UsageStreamKeys.TotalTokens;
    public const string CacheCreationInputTokens = UsageStreamKeys.CacheCreationInputTokens;
    public const string CacheReadInputTokens = UsageStreamKeys.CacheReadInputTokens;
    public const string OutputImageTokens = UsageStreamKeys.OutputImageTokens;
    public const string OutputAudioTokens = UsageStreamKeys.OutputAudioTokens;
    public const string OutputVideoTokens = UsageStreamKeys.OutputVideoTokens;
    public const string ReasoningTokens = UsageStreamKeys.ReasoningTokens;

    public static readonly string[] InputCandidates = UsageStreamKeys.LegacyInputCandidates;
    public static readonly string[] OutputCandidates = UsageStreamKeys.LegacyOutputCandidates;
    public static readonly string[] TotalCandidates = [TotalTokens, "total_tokens"];
    public static readonly string[] CacheCreationInputCandidates =
        [CacheCreationInputTokens, "cache_creation_input_tokens", "cacheCreationInputTokens"];
    public static readonly string[] CacheReadInputCandidates =
        [CacheReadInputTokens, "cache_read_input_tokens", "cacheReadInputTokens"];

    public static Dictionary<string, object> Build(
        long? inputTokens,
        long? outputTokens,
        long? totalTokens = null,
        long? cacheCreationInputTokens = null,
        long? cacheReadInputTokens = null,
        long? outputImageTokens = null,
        long? outputAudioTokens = null,
        long? outputVideoTokens = null,
        long? reasoningTokens = null)
    {
        var usage = new Dictionary<string, object>();
        if (inputTokens.HasValue) usage[InputTokens] = inputTokens.Value;
        if (outputTokens.HasValue) usage[OutputTokens] = outputTokens.Value;
        if (cacheCreationInputTokens.HasValue) usage[CacheCreationInputTokens] = cacheCreationInputTokens.Value;
        if (cacheReadInputTokens.HasValue) usage[CacheReadInputTokens] = cacheReadInputTokens.Value;
        if (outputImageTokens is > 0) usage[OutputImageTokens] = outputImageTokens.Value;
        if (outputAudioTokens is > 0) usage[OutputAudioTokens] = outputAudioTokens.Value;
        if (outputVideoTokens is > 0) usage[OutputVideoTokens] = outputVideoTokens.Value;
        if (reasoningTokens is > 0) usage[ReasoningTokens] = reasoningTokens.Value;
        if (totalTokens.HasValue) usage[TotalTokens] = totalTokens.Value;
        else if (inputTokens.HasValue && outputTokens.HasValue)
            usage[TotalTokens] = inputTokens.Value + outputTokens.Value +
                                (cacheCreationInputTokens ?? 0) + (cacheReadInputTokens ?? 0);
        return usage;
    }

    public static CandidateModalityTokens SumCandidateModalityTokens(JsonElement usageMetadata)
    {
        long image = 0, audio = 0, video = 0;
        if (!usageMetadata.TryGetProperty("candidatesTokensDetails", out JsonElement details) ||
            details.ValueKind != JsonValueKind.Array)
            return default;

        foreach (JsonElement item in details.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            string? modality = item.TryGetProperty("modality", out JsonElement m) ? m.GetString() : null;
            long count = ReadNumber(item, "tokenCount") ?? 0;
            switch (modality?.ToUpperInvariant())
            {
                case "IMAGE": image += count; break;
                case "AUDIO": audio += count; break;
                case "VIDEO": video += count; break;
            }
        }

        return new CandidateModalityTokens(image, audio, video);
    }

    public static long? ReadNumber(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!obj.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number)
                continue;
            if (el.TryGetInt64(out var value))
                return value;
        }
        return null;
    }
}
