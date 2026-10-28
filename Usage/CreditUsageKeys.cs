namespace OneApi.Common.Usage;

/// <summary>
/// 上游积分的用量键。生图随 <see cref="Dispatch.ImageBodyChunk.Usage"/> 带回，对话随 <see cref="Dispatch.StreamingChunkDto.Usage"/>（Kiro 的 <c>credits</c>）带回，同一套键。
/// token 键在 <see cref="UsageStreamKeys"/>；积分不是 token，单列。
/// 「积分」是上游厂商自己的计量单位（NovelAI 叫 Anlas），各家各算各的，网关只认这个数。
/// </summary>
public static class CreditUsageKeys
{
    /// <summary>
    /// 这次生成按上游标价要扣的积分（NovelAI 由 <see cref="NovelAiAnlas"/> 算）。
    /// 计费按它：客户买的是这次生成本身的价值，号池账号是不是免费档与客户无关。
    /// </summary>
    public const string Credits = "credits";

    /// <summary>
    /// 号池账号实际被扣的积分。免费档时为 0；账号档位未知时不带。
    /// 只用于对账和成本核算，不参与客户计费。
    /// </summary>
    public const string CreditsCharged = "creditsCharged";
}
