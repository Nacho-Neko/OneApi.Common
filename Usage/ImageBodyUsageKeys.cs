namespace OneApi.Common.Usage;

/// <summary>
/// 生图回包随 <see cref="Dispatch.ImageBodyChunk.Usage"/> 带回的用量键。
/// 对话的 token 键在 <see cref="UsageStreamKeys"/>；生图上游不报 token，这里是另一套。
/// </summary>
public static class ImageBodyUsageKeys
{
    /// <summary>
    /// 这次生成按 NovelAI 标价要扣的 Anlas（<see cref="NovelAiAnlas"/>）。
    /// 计费按它：客户买的是这次生成本身的价值，号池账号是不是 Opus 免费与客户无关。
    /// </summary>
    public const string Anlas = "anlas";

    /// <summary>
    /// 号池账号实际被扣的 Anlas。Opus 免费时为 0；账号档位未知时不带。
    /// 只用于对账和成本核算，不参与客户计费。
    /// </summary>
    public const string AnlasCharged = "anlasCharged";
}
