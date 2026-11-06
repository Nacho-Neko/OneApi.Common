namespace OneApi.Common.Catalog;

/// <summary>
/// Cross-provider model id parsing: bracket params, variant suffixes, billing tiers.
/// Provider-specific wire mapping (e.g. Cursor bracket syntax) stays in each Provider.
/// </summary>
public static class ModelSlug
{
    private static readonly HashSet<string> ReservedProductSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "gpt-5.1-codex-max",
    };

    public static string ResolveBillingTier(string model)
    {
        string slug = SplitBracketSyntax(model).BaseSlug;
        if (string.IsNullOrWhiteSpace(slug))
            return string.Empty;

        bool isFast = slug.EndsWith(ModelVariantTokens.Fast, StringComparison.Ordinal);
        string core = isFast
            ? slug[..^ModelVariantTokens.Fast.Length]
            : slug;

        core = StripVariantSuffixes(core);

        if (string.IsNullOrEmpty(core))
            return slug;

        return isFast ? core + ModelVariantTokens.Fast : core;
    }

    public static string StripToProductBase(string slug)
    {
        string core = slug;
        if (core.EndsWith(ModelVariantTokens.Fast, StringComparison.Ordinal))
            core = core[..^ModelVariantTokens.Fast.Length];

        return StripVariantSuffixes(core);
    }

    private static string StripVariantSuffixes(string core)
    {
        if (ReservedProductSlugs.Contains(core))
            return core;

        ModelVariantTokens.TryStripFromTail(core, ModelVariantTokens.Thinking, out core);
        if (ReservedProductSlugs.Contains(core))
            return core;

        ModelVariantTokens.TryStripFromTail(core, ModelVariantTokens.Effort, out core);
        return core;
    }

    public static BracketSplit SplitBracketSyntax(string model)
    {
        string s = (model ?? string.Empty).Trim();
        int open = s.IndexOf('[', StringComparison.Ordinal);
        if (open <= 0 || !s.EndsWith(']'))
            return new BracketSplit(s, null);

        string baseSlug = s[..open].Trim();
        string inner = s[(open + 1)..^1].Trim();
        return new BracketSplit(baseSlug, string.IsNullOrEmpty(inner) ? null : inner);
    }

    public static ModelSlugParts Parse(string model)
    {
        string raw = (model ?? string.Empty).Trim();
        BracketSplit split = SplitBracketSyntax(raw);
        string slug = split.BaseSlug;
        bool fast = slug.EndsWith(ModelVariantTokens.Fast, StringComparison.Ordinal);
        string billingTier = ResolveBillingTier(raw);
        string productBase = StripToProductBase(
            fast ? slug[..^ModelVariantTokens.Fast.Length] : slug);

        return new ModelSlugParts(raw, slug, productBase, billingTier, fast, split.BracketParams);
    }
}
