namespace OneApi.Common.Usage;

/// <summary>
/// 上游积分的用量键。生图随 <see cref="Dispatch.ImageBodyChunk.Usage"/> 带回，
/// 对话随 <see cref="Dispatch.StreamingChunkDto.Usage"/>（Kiro 的 <c>credits</c>）带回，同一套键。
/// token 键在 <see cref="UsageStreamKeys"/>；积分不是 token，单列。
/// 「积分」是上游厂商自己的计量单位（NovelAI 叫 Anlas），各家各算各的，网关只认这个数。
/// </summary>
public static class CreditUsageKeys
{
    /// <summary>
    /// 这次调用实际用掉的积分。Kiro 由上游流里的 meteringEvent 给；NovelAI 上游不回，
    /// Provider 按号池账号的档位和请求参数算出账号实际被扣的数（<see cref="NovelAiAnlas"/>）。
    /// 网关不估、不改，原样交给控制面按每积分单价扣钱。
    /// </summary>
    public const string Credits = "credits";
}
