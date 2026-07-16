using System.Text.Json.Serialization;

namespace OneApi.Common.Usage;

// usage json 的标准形状（jsonb 落库 / API 透传均用此形）。
// token 形与 DemuxAi usage_logs.usage（LogUsageDto 系）字段名、嵌套、顺序完全一致；
// 媒体形与 DemuxAi per_image / per_video / per_audio_minute 的用量 DTO 语义一致。

/// <summary>token 形 usage json：{totalTokens, input{…}, output{…}}。</summary>
public sealed class TokenUsageDocument
{
    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("input")]
    public TokenUsageInput Input { get; set; } = new();

    [JsonPropertyName("output")]
    public TokenUsageOutput Output { get; set; } = new();
}

public sealed class TokenUsageInput
{
    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }

    [JsonPropertyName("cachedReadTokens")]
    public int CachedReadTokens { get; set; }

    [JsonPropertyName("cachedWriteTokens")]
    public int CachedWriteTokens { get; set; }

    [JsonPropertyName("audioTokens")]
    public int AudioTokens { get; set; }
}

public sealed class TokenUsageOutput
{
    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }

    /// <summary>思考 / reasoning token。</summary>
    [JsonPropertyName("reasoningTokens")]
    public int ReasoningTokens { get; set; }

    [JsonPropertyName("audioTokens")]
    public int AudioTokens { get; set; }
}

/// <summary>image 形 usage json：{tier{size, quality}, count}。</summary>
public sealed class ImageUsageDocument
{
    [JsonPropertyName("tier")]
    public ImageUsageTier Tier { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public sealed class ImageUsageTier
{
    [JsonPropertyName("size")]
    public string Size { get; set; } = "unknown";

    [JsonPropertyName("quality")]
    public string Quality { get; set; } = "default";
}

/// <summary>audio 形 usage json：{seconds}。计费侧按分钟折算（per_audio_minute）。</summary>
public sealed class AudioUsageDocument
{
    [JsonPropertyName("seconds")]
    public decimal Seconds { get; set; }
}

/// <summary>video 形 usage json：{tier{resolution}, seconds}。</summary>
public sealed class VideoUsageDocument
{
    [JsonPropertyName("tier")]
    public VideoUsageTier Tier { get; set; } = new();

    [JsonPropertyName("seconds")]
    public decimal Seconds { get; set; }
}

public sealed class VideoUsageTier
{
    [JsonPropertyName("resolution")]
    public string Resolution { get; set; } = "unknown";
}
