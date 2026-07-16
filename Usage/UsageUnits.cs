namespace OneApi.Common.Usage;

/// <summary>
/// 计量单位常量。平台基线定价（Tavern <c>metering_pricing</c>）、
/// 回合 commit（<c>TavernModelUsage.Unit</c>）与 usage kind 映射均以此为准。
/// </summary>
public static class UsageUnits
{
    public const string Token = "token";
    public const string Image = "image";
    public const string AudioSecond = "audio_second";
    public const string VideoSecond = "video_second";

    /// <summary>按调用类别推导默认计量单位（<see cref="UsageKinds"/> → unit）。</summary>
    public static string ForKind(string kind) => kind switch
    {
        UsageKinds.Image => Image,
        UsageKinds.Audio => AudioSecond,
        UsageKinds.Video => VideoSecond,
        _ => Token,
    };

    public static bool IsKnown(string? unit) =>
        unit is Token or Image or AudioSecond or VideoSecond;
}
