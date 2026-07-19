namespace OneApi.Common.Catalog;

/// <summary>
/// Global model-id variant suffixes. Any provider may encode runtime knobs on the
/// public model slug (<c>-low</c>, <c>-thinking-high</c>, <c>-fast</c>, …).
/// Entry layers use these for billing-tier normalization; each Provider decides
/// whether to strip them into dedicated request fields or pass the slug through.
/// </summary>
public static class ModelVariantTokens
{
    public const string EffortExtraHigh = "-extra-high";
    public const string EffortXHigh     = "-xhigh";
    public const string EffortMedium    = "-medium";
    public const string EffortHigh      = "-high";
    public const string EffortLow       = "-low";
    public const string EffortMax       = "-max";
    public const string EffortNone      = "-none";

    public static readonly string[] Effort =
    [
        EffortExtraHigh,
        EffortXHigh,
        EffortMedium,
        EffortHigh,
        EffortLow,
        EffortMax,
        EffortNone,
    ];

    public const string ThinkingExtraHigh = "-thinking-extra-high";
    public const string ThinkingXHigh     = "-thinking-xhigh";
    public const string ThinkingMedium    = "-thinking-medium";
    public const string ThinkingHigh      = "-thinking-high";
    public const string ThinkingLow       = "-thinking-low";
    public const string ThinkingMax       = "-thinking-max";
    public const string ThinkingNone      = "-thinking-none";

    public static readonly string[] Thinking =
    [
        ThinkingExtraHigh,
        ThinkingXHigh,
        ThinkingMedium,
        ThinkingHigh,
        ThinkingLow,
        ThinkingMax,
        ThinkingNone,
        "-thinking",
    ];

    public const string Fast = "-fast";

    public static readonly string[] All =
        Thinking.Concat(Effort).Append(Fast).ToArray();

    public static bool TryStripFromTail(string slug, IReadOnlyList<string> tokens, out string stripped)
    {
        foreach (var token in tokens)
        {
            if (!slug.EndsWith(token, StringComparison.Ordinal))
                continue;
            stripped = slug[..^token.Length];
            return true;
        }
        stripped = slug;
        return false;
    }
}
