namespace OneApi.Common.Usage;

/// <summary>
/// Demux 模型定价计费类型（<c>model_pricings.billing_type</c>）常量。
/// usage_json 形状与 commit 计量方式均由此决定。
/// </summary>
public static class BillingTypes
{
    // ── 跨模态通用：同一种方式可以挂在对话、生图、视频等任何模型上 ──
    public const string PerToken = "per_token";
    public const string PerCall = "per_call";

    /// <summary>
    /// 按上游积分计价（NovelAI 叫 Anlas，Kiro 叫 credits）：rate_json 是 <c>{ pricePerCredit }</c>。
    /// 用量是独立的积分口径，积分可以是小数；不预估，预扣只查余额，结算按上游返回的实际积分扣。
    /// </summary>
    public const string PerCredit = "per_credit";

    // ── 模态专属：只对应一种产出 ──
    public const string PerImage = "per_image";
    public const string PerVideo = "per_video";
    public const string PerAudioMinute = "per_audio_minute";
    public const string PerCharacter = "per_character";

    /// <summary>归一 billing_type；非法抛 <see cref="ArgumentException"/>。</summary>
    public static string Normalize(string? raw)
    {
        var bt = (raw ?? PerToken).Trim().ToLowerInvariant();
        return bt switch
        {
            PerToken or PerCall or PerCredit or PerImage or PerVideo or PerAudioMinute or PerCharacter => bt,
            _ => throw new ArgumentException($"unsupported billing_type '{raw}'.", nameof(raw)),
        };
    }

    /// <summary>该 billing_type 的 usage_json 是否为 token 形（可经 <see cref="UsageJsonCodec"/> 解析）。</summary>
    public static bool IsTokenShaped(string billingType) =>
        billingType is PerToken or PerCall;
}
