using System.Text.Json.Serialization;

namespace OneApi.Common.Usage;

/// <summary>
/// NovelAI <c>POST /ai/generate-image</c> 请求体里估价用得到的那部分。提示词、种子、采样器这些与扣费无关，
/// 不声明，反序列化时跳过。
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed record NovelAiImageRequest
{
    /// <summary><c>generate</c> / <c>img2img</c> / <c>infill</c>；有的客户端写在 parameters 里。</summary>
    [JsonPropertyName("action")] public string? Action { get; init; }

    [JsonPropertyName("parameters")] public NovelAiImageParameters? Parameters { get; init; }
}

[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed record NovelAiImageParameters
{
    [JsonPropertyName("action")] public string? Action { get; init; }

    [JsonPropertyName("width")] public int? Width { get; init; }
    [JsonPropertyName("height")] public int? Height { get; init; }
    [JsonPropertyName("steps")] public int? Steps { get; init; }
    [JsonPropertyName("n_samples")] public int? Samples { get; init; }

    [JsonPropertyName("sm")] public bool? Smea { get; init; }
    [JsonPropertyName("sm_dyn")] public bool? SmeaDyn { get; init; }

    /// <summary>img2img 的去噪强度，只在有底图时生效。</summary>
    [JsonPropertyName("strength")] public double? Strength { get; init; }

    [JsonPropertyName("uncond_scale")] public double? UncondScale { get; init; }

    /// <summary>底图（base64）。有它就不是纯文生图，没有 Opus 免费。</summary>
    [JsonPropertyName("image")] public string? Image { get; init; }
}
