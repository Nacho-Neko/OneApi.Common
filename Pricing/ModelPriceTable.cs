using System.Security.Cryptography;
using System.Text;

namespace OneApi.Common.Pricing;

/// <summary>
/// 某一刻的整张价格表，不可变。产品拿它把自己的模型名对到牌价上，再乘自己的倍率算有效价。
/// <para>
/// 一张空表不等于「所有模型都没有价格」：号池后台还没发布过、桶被清了、还没连上 NATS，手上的都是空表。
/// 所以按它重算有效价的一方，对不上的模型要保留原来的有效价，不能当成「下架」。
/// </para>
/// <para>
/// 对牌价时认的名字是每一行的键，加上它的别名（<see cref="PublishedModelPrice.Aliases"/>）。
/// 键和别名撞了（发布方的错）键优先：别名不会把别的行的键抢过去。
/// </para>
/// </summary>
public sealed class ModelPriceTable
{
    public static readonly ModelPriceTable Empty = new([], 0);

    private readonly Dictionary<string, PublishedModelPrice> _byKey;
    private readonly string[] _keys;

    /// <summary>键和别名 → 那一行。</summary>
    private readonly Dictionary<string, PublishedModelPrice> _byName;
    private readonly string[] _names;

    public ModelPriceTable(IEnumerable<PublishedModelPrice> prices, ulong revision)
    {
        _byKey = new Dictionary<string, PublishedModelPrice>(StringComparer.Ordinal);
        foreach (var price in prices)
        {
            if (string.IsNullOrWhiteSpace(price.ModelKey)) continue;
            // 同一个键出现在两个厂家的文档里是发布方的错；后到的不盖先到的，结果至少是确定的（按厂家名排过序再喂进来）
            _byKey.TryAdd(Normalize(price.ModelKey), price);
        }
        _keys = [.. _byKey.Keys];

        // 先放全部的键、再放别名：别名和别的行的键撞了，键赢；两行的别名撞了，键排在前面的那一行赢（结果是确定的）
        _byName = new Dictionary<string, PublishedModelPrice>(_byKey, StringComparer.Ordinal);
        foreach (var (_, price) in _byKey.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (var alias in price.Aliases ?? [])
            {
                if (string.IsNullOrWhiteSpace(alias)) continue;
                _byName.TryAdd(Normalize(alias), price);
            }
        }
        _names = [.. _byName.Keys];

        Revision = revision;
        Fingerprint = ComputeFingerprint(_byKey);
    }

    /// <summary>
    /// 拼出这张表时桶里最新一条的修订号，只增不减，用来说「我手上是第几版」。
    /// 判断「价格变没变」看 <see cref="Fingerprint"/>：同一份内容重发一遍修订号也会涨。
    /// </summary>
    public ulong Revision { get; }

    /// <summary>内容的指纹：行的集合、每行的计价方式、价格和别名都一样，指纹就一样。</summary>
    public string Fingerprint { get; }

    public int Count => _byKey.Count;
    public bool IsEmpty => _byKey.Count == 0;
    public IReadOnlyCollection<PublishedModelPrice> Prices => _byKey.Values;

    /// <summary>每一行的键（不含别名）。</summary>
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>对牌价时认的全部名字：每一行的键和别名。</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>键或者别名全等（归一后）。</summary>
    public PublishedModelPrice? Find(string modelKey)
        => !string.IsNullOrWhiteSpace(modelKey) && _byName.TryGetValue(Normalize(modelKey), out var price) ? price : null;

    /// <summary>
    /// 上游的模型名对到表里的那一行：键和别名一起，最长前缀（<see cref="ModelKeyMatcher.FindBestKey"/>）。
    /// <c>claude-3-haiku-20240307</c> 对 <c>claude-3-haiku</c>；<c>gemini-3.1-pro</c> 对别名是它的 <c>gemini-3.1-pro-preview</c>。对不上回 null。
    /// </summary>
    public PublishedModelPrice? FindBest(string vendorModel)
    {
        if (string.IsNullOrWhiteSpace(vendorModel) || _names.Length == 0) return null;
        var name = ModelKeyMatcher.FindBestKey(vendorModel, _names);
        return name is not null && _byName.TryGetValue(name, out var price) ? price : null;
    }

    /// <summary>
    /// 同 <see cref="FindBest"/>，回的是那一行的键（归一后）：经别名对上的也回行的键，不回别名——
    /// 按键记账的（有效价记着照哪个键算的、按键数路由）记的是行，不是对上它的那个名字。
    /// </summary>
    public string? FindBestKey(string vendorModel)
        => FindBest(vendorModel) is { } price ? Normalize(price.ModelKey) : null;

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();

    private static string ComputeFingerprint(Dictionary<string, PublishedModelPrice> byKey)
    {
        if (byKey.Count == 0) return "empty";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (key, price) in byKey.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(key));
            hash.AppendData([0]);
            hash.AppendData(Encoding.UTF8.GetBytes(price.BillingType ?? string.Empty));
            hash.AppendData([0]);
            hash.AppendData(Encoding.UTF8.GetBytes(price.RateJson));
            hash.AppendData([0]);
            // 别名变了，对得上的模型就变了，各产品要重算。没有别名的行不往里加东西：没设别名的表，指纹和加别名之前一样，
            // 换新版的那一刻不会让各产品白重算一遍
            if (price.Aliases is { Count: > 0 } aliases)
            {
                hash.AppendData([1]);
                foreach (var alias in aliases.Select(Normalize).Order(StringComparer.Ordinal))
                {
                    hash.AppendData(Encoding.UTF8.GetBytes(alias));
                    hash.AppendData([0]);
                }
            }
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
