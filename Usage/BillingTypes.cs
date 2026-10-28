namespace OneApi.Common.Usage;

/// <summary>
/// Demux 模型定价计费类型（<c>model_pricings.billing_type</c>）常量。
/// usage_json 形状与 commit 计量方式均由此决定。
/// </summary>
public static class BillingTypes
{
    public const string PerToken = "per_token";
    public const string PerCall = "per_call";
    public const string PerImage = "per_image";
    public const string PerVideo = "per_video";
    public const string PerAudioMinute = "per_audio_minute";
    public const string PerCharacter = "per_character";

    /// <summary>
    /// 按上游积分计价（NovelAI 叫 Anlas）：rate_json 是 <c>{ pricePerCredit }</c>，用量仍是生图口径
    /// （<c>ImageUsage.Credits</c>），张数照记只是不再定价。
    /// </summary>
    public const string PerCredit = "per_credit";

    /// <summary>归一 billing_type；非法抛 <see cref="ArgumentException"/>。</summary>
    public static string Normalize(string? raw)
    {
        var bt = (raw ?? PerToken).Trim().ToLowerInvariant();
        return bt switch
        {
            PerToken or PerCall or PerImage or PerVideo or PerAudioMinute or PerCharacter or PerCredit => bt,
            _ => throw new ArgumentException($"unsupported billing_type '{raw}'.", nameof(raw)),
        };
    }

    /// <summary>该 billing_type 的 usage_json 是否为 token 形（可经 <see cref="UsageJsonCodec"/> 解析）。</summary>
    public static bool IsTokenShaped(string billingType) =>
        billingType is PerToken or PerCall;
}
