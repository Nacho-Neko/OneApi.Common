using System.Text.RegularExpressions;

namespace OneApi.Common.Pricing;

/// <summary>
/// 价格表键与上游 / 路由模型名的归一。
/// 表里只存厂家发布的干净名（<c>claude-3-haiku</c>、<c>claude-fable-5.1</c>），
/// 不要渠道前缀、不要日期钉。<c>vendor_model</c> 用最长前缀对上表里的键：
/// <c>claude-opus4-8</c> 对 <c>claude-opus-4-8</c>，<c>claude-3-haiku-20240307</c> 对 <c>claude-3-haiku</c>。
/// </summary>
public static partial class ModelKeyMatcher
{
    /// <summary>
    /// 写入价格表的键：剥渠道前缀 / 日期 / 变体后缀，保留厂家自己的点号版本（5.1）。
    /// 路由别名（<c>~…</c>、<c>-latest</c>）返回空，调用方应跳过。
    /// </summary>
    public static string CanonicalId(string raw)
    {
        if (IsRoutingAlias(raw)) return string.Empty;
        var s = StripDecorators(raw, stripDates: true);
        if (s.Length == 0 || s.EndsWith("-latest", StringComparison.Ordinal)) return string.Empty;
        return s;
    }

    /// <summary>Compact 形式，用来把 <c>claude-opus-4-8</c> 和 <c>claude-opus4-8</c> 收成同一行。</summary>
    public static string Compact(string raw)
    {
        var s = Fold(raw);
        if (s.Length == 0) return string.Empty;
        return s.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// 路由名能否用这条价格键计价。相等、紧跟 <c>-</c> 的前缀、或去掉横线点号后全等。
    /// <c>gpt-4o-mini</c> 不会仅因前缀对上 <c>gpt-4o</c> 之外的更长短语——最长键由
    /// <see cref="FindBestKey"/> 挑。
    /// </summary>
    public static bool Matches(string vendorModel, string priceKey)
    {
        var foldedVendor = Fold(vendorModel);
        var foldedKey = Fold(priceKey);
        if (foldedVendor.Length == 0 || foldedKey.Length == 0) return false;
        if (string.Equals(foldedVendor, foldedKey, StringComparison.Ordinal)) return true;
        if (foldedVendor.StartsWith(foldedKey + "-", StringComparison.Ordinal)) return true;

        var compactVendor = Compact(foldedVendor);
        var compactKey = Compact(foldedKey);
        return compactVendor.Length > 0 && string.Equals(compactVendor, compactKey, StringComparison.Ordinal);
    }

    /// <summary>在价格键里挑最长命中；一样长时精确折叠优于 compact。</summary>
    public static string? FindBestKey(string vendorModel, IReadOnlyList<string> priceKeys)
    {
        string? best = null;
        var bestLen = -1;
        var bestExact = false;
        foreach (var key in priceKeys)
        {
            if (!Matches(vendorModel, key)) continue;
            var exact = string.Equals(Fold(vendorModel), Fold(key), StringComparison.Ordinal)
                        || string.Equals(Compact(vendorModel), Compact(key), StringComparison.Ordinal);
            if (best is null
                || key.Length > bestLen
                || (key.Length == bestLen && exact && !bestExact))
            {
                best = key;
                bestLen = key.Length;
                bestExact = exact;
            }
        }
        return best;
    }

    /// <summary>
    /// 旧的宽松相等：点横线、日期、provider 前缀折完再比。给拉价 diff 把候选挂到已有行用；
    /// 不把 <c>gpt-4o-mini</c> 挂到 <c>gpt-4o</c>。
    /// </summary>
    public static string Normalize(string raw)
    {
        var id = CanonicalId(raw);
        if (id.Length == 0) id = StripDecorators(raw, stripDates: true);
        return id.Replace('.', '-').Replace('_', '-');
    }

    /// <summary>
    /// 拉价时把上游的一条候选挂到表里已有的行上。exact：小写全等；normalized：干净键或 compact 相等；none：不匹配。
    /// </summary>
    /// <param name="candidateModelId">候选的模型 id（不含 provider 前缀）。</param>
    /// <param name="candidateSourceRef">候选在上游里的原始标识（如 <c>anthropic/claude-sonnet-4-5</c>）。</param>
    public static string Match(string localModelKey, string candidateModelId, string candidateSourceRef)
    {
        var local = localModelKey.Trim().ToLowerInvariant();
        var remote = candidateModelId.Trim().ToLowerInvariant();
        if (string.Equals(local, remote, StringComparison.Ordinal)
            || string.Equals(local, candidateSourceRef.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            return MatchKinds.Exact;

        var lc = CanonicalId(local);
        var rc = CanonicalId(candidateModelId);
        if (lc.Length > 0 && string.Equals(lc, rc, StringComparison.Ordinal))
            return MatchKinds.Normalized;
        if (lc.Length > 0 && rc.Length > 0 && string.Equals(Compact(lc), Compact(rc), StringComparison.Ordinal))
            return MatchKinds.Normalized;
        return MatchKinds.None;
    }

    public static bool IsRoutingAlias(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var s = raw.Trim();
        return s.StartsWith('~') || s.StartsWith("~/", StringComparison.Ordinal);
    }

    /// <summary>剥前缀后的叶子，日期钉还在，给前缀匹配用。</summary>
    public static string Leaf(string raw) => StripDecorators(raw, stripDates: false);

    private static string Fold(string raw)
    {
        var s = StripDecorators(raw, stripDates: false);
        if (s.Length == 0) return string.Empty;
        s = s.Replace('.', '-').Replace('_', '-');
        return MultiDash().Replace(s, "-").Trim('-');
    }

    private static string StripDecorators(string raw, bool stripDates)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim().ToLowerInvariant();
        if (s.StartsWith('~')) s = s[1..];

        var slash = s.LastIndexOf('/');
        if (slash >= 0) s = s[(slash + 1)..];

        var dashDash = s.LastIndexOf("--", StringComparison.Ordinal);
        if (dashDash >= 0) s = s[(dashDash + 2)..];

        if (s.StartsWith("models/", StringComparison.Ordinal)) s = s["models/".Length..];

        s = StripMakerDotPrefix(s);

        var colon = s.IndexOf(':');
        if (colon >= 0) s = s[..colon];
        var at = s.IndexOf('@');
        if (at >= 0) s = s[..at];

        if (stripDates)
        {
            s = DateSuffix().Replace(s, string.Empty);
            s = HyphenatedDateSuffix().Replace(s, string.Empty);
            s = VersionSuffix().Replace(s, string.Empty);
            s = LatestSuffix().Replace(s, string.Empty);
        }

        s = MultiDash().Replace(s, "-").Trim('-');
        return s;
    }

    /// <summary><c>us.anthropic.claude-sonnet-4-5</c> → <c>claude-sonnet-4-5</c>。</summary>
    private static string StripMakerDotPrefix(string s)
    {
        var dot = s.IndexOf('.');
        if (dot <= 0) return s;

        var best = -1;
        foreach (var token in MakerCatalog.KnownTokens())
        {
            var needle = "." + token + ".";
            var i = s.LastIndexOf(needle, StringComparison.Ordinal);
            if (i >= 0 && i + needle.Length > best)
                best = i + needle.Length;
            if (s.StartsWith(token + ".", StringComparison.Ordinal) && token.Length + 1 > best)
                best = token.Length + 1;
        }
        return best > 0 ? s[best..] : s;
    }

    [GeneratedRegex(@"-20\d{6}$")]
    private static partial Regex DateSuffix();

    [GeneratedRegex(@"-20\d{2}-\d{2}-\d{2}$")]
    private static partial Regex HyphenatedDateSuffix();

    [GeneratedRegex(@"-v\d+$")]
    private static partial Regex VersionSuffix();

    [GeneratedRegex(@"-latest$")]
    private static partial Regex LatestSuffix();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDash();
}

public static class MatchKinds
{
    public const string Exact      = "exact";
    public const string Normalized = "normalized";
    public const string None       = "none";
}
