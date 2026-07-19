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
        var slug = SplitBracketSyntax(model).BaseSlug;
        if (string.IsNullOrWhiteSpace(slug))
            return string.Empty;

        var isFast = slug.EndsWith(ModelVariantTokens.Fast, StringComparison.Ordinal);
        var core = isFast
            ? slug[..^ModelVariantTokens.Fast.Length]
            : slug;

        core = StripVariantSuffixes(core);

        if (string.IsNullOrEmpty(core))
            return slug;

        return isFast ? core + ModelVariantTokens.Fast : core;
    }

    public static string StripToProductBase(string slug)
    {
        var core = slug;
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

    public static (string BaseSlug, string? BracketParams) SplitBracketSyntax(string model)
    {
        var s = (model ?? string.Empty).Trim();
        var open = s.IndexOf('[', StringComparison.Ordinal);
        if (open <= 0 || !s.EndsWith(']'))
            return (s, null);

        var baseSlug = s[..open].Trim();
        var inner = s[(open + 1)..^1].Trim();
        return (baseSlug, string.IsNullOrEmpty(inner) ? null : inner);
    }

    public static ModelSlugParts Parse(string model)
    {
        var raw = (model ?? string.Empty).Trim();
        var (slug, bracketParams) = SplitBracketSyntax(raw);
        var fast = slug.EndsWith(ModelVariantTokens.Fast, StringComparison.Ordinal);
        var billingTier = ResolveBillingTier(raw);
        var productBase = StripToProductBase(
            fast ? slug[..^ModelVariantTokens.Fast.Length] : slug);

        return new ModelSlugParts(raw, slug, productBase, billingTier, fast, bracketParams);
    }
}

public record ModelSlugParts(
    string Raw,
    string SlugWithoutBrackets,
    string ProductBase,
    string BillingTier,
    bool Fast,
    string? BracketParams);
