using System.Text.RegularExpressions;

namespace OneApi.Common.Vendors;

/// <summary>
/// 模型路由标识的公共规范（唯一定义处）。DemuxAi 目录域（vendors / model_aliases）与
/// Tavern 会话 / 用量表都以此为准，防止各服务对 slug / 模型名各写一套长度与格式规则。
/// <list type="bullet">
///   <item><b>渠道 slug</b>（vendor_slug / queue_group / vendor_plug / vendor_key）：
///     小写归一，格式 <see cref="SlugPattern"/>（首字母小写、2~63 位、仅 a-z 0-9 _ -）。</item>
///   <item><b>vendor_model</b>（上游真实模型名）：trim，最长 <see cref="VendorModelMaxLength"/>，大小写保留。</item>
///   <item><b>alias</b>（对外别名）：trim，最长 <see cref="AliasMaxLength"/>，大小写保留。</item>
/// </list>
/// </summary>
public static partial class VendorRouting
{
    /// <summary>渠道 slug 格式：^[a-z][a-z0-9_-]{1,62}$（与 NATS queue group 命名兼容）。</summary>
    public const string SlugPattern = "^[a-z][a-z0-9_-]{1,62}$";

    /// <summary>渠道 slug 最大长度（列宽建议 64）。</summary>
    public const int SlugMaxLength = 63;

    /// <summary>上游模型名最大长度（列宽建议 160）。</summary>
    public const int VendorModelMaxLength = 160;

    /// <summary>对外别名最大长度（列宽建议 128）。</summary>
    public const int AliasMaxLength = 128;

    [GeneratedRegex(SlugPattern)]
    private static partial Regex SlugRegex();

    /// <summary>slug 是否合法（已归一的小写形式）。</summary>
    public static bool IsValidSlug(string slug) => SlugRegex().IsMatch(slug);

    /// <summary>归一渠道 slug：trim + 小写；非法抛 <see cref="ArgumentException"/>。空白返回 null。</summary>
    public static string? NormalizeSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var slug = raw.Trim().ToLowerInvariant();
        if (!IsValidSlug(slug))
            throw new ArgumentException($"vendor slug must match {SlugPattern}", nameof(raw));
        return slug;
    }

    /// <summary>宽松版归一：非法返回 null 而不抛错（导入 / 查询等容错场景）。</summary>
    public static string? TryNormalizeSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var slug = raw.Trim().ToLowerInvariant();
        return IsValidSlug(slug) ? slug : null;
    }

    /// <summary>归一必填渠道 slug（vendor_key 等不可空场景）；空或非法抛 <see cref="ArgumentException"/>。</summary>
    public static string NormalizeRequiredSlug(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("vendor slug is required.", nameof(raw));
        return NormalizeSlug(raw)!;
    }

    /// <summary>归一上游模型名：trim；空或超长抛 <see cref="ArgumentException"/>。</summary>
    public static string NormalizeVendorModel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("vendor_model is required.", nameof(raw));
        var trimmed = raw.Trim();
        if (trimmed.Length > VendorModelMaxLength)
            throw new ArgumentException($"vendor_model exceeds max length {VendorModelMaxLength}.", nameof(raw));
        return trimmed;
    }

    /// <summary>归一对外别名：trim；空或超长抛 <see cref="ArgumentException"/>。</summary>
    public static string NormalizeAlias(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("alias is required.", nameof(raw));
        var trimmed = raw.Trim();
        if (trimmed.Length > AliasMaxLength)
            throw new ArgumentException($"alias exceeds max length {AliasMaxLength}.", nameof(raw));
        return trimmed;
    }

    /// <summary>宽松版别名归一：空白 / 超长返回 null 而不抛错（客户端上行等容错场景）。</summary>
    public static string? TryNormalizeAlias(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var trimmed = raw.Trim();
        return trimmed.Length <= AliasMaxLength ? trimmed : null;
    }
}
