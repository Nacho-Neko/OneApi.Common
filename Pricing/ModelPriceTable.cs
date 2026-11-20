using System.Security.Cryptography;
using System.Text;

namespace OneApi.Common.Pricing;

/// <summary>
/// 某一刻的整张价格表，不可变。产品拿它把自己的模型名对到牌价上，再乘自己的倍率算有效价。
/// <para>
/// 一张空表不等于「所有模型都没有价格」：号池后台还没发布过、桶被清了、还没连上 NATS，手上的都是空表。
/// 所以按它重算有效价的一方，对不上的模型要保留原来的有效价，不能当成「下架」。
/// </para>
/// </summary>
public sealed class ModelPriceTable
{
    public static readonly ModelPriceTable Empty = new([], 0);

    private readonly Dictionary<string, PublishedModelPrice> _byKey;
    private readonly string[] _keys;

    public ModelPriceTable(IEnumerable<PublishedModelPrice> prices, ulong revision)
    {
        _byKey = new Dictionary<string, PublishedModelPrice>(StringComparer.Ordinal);
        foreach (var price in prices)
        {
            if (string.IsNullOrWhiteSpace(price.ModelKey)) continue;
            // 同一个键出现在两个厂家的文档里是发布方的错；后到的不盖先到的，结果至少是确定的（按厂家名排过序再喂进来）
            _byKey.TryAdd(price.ModelKey.Trim().ToLowerInvariant(), price);
        }
        _keys = [.. _byKey.Keys];
        Revision = revision;
        Fingerprint = ComputeFingerprint(_byKey);
    }

    /// <summary>
    /// 拼出这张表时桶里最新一条的修订号，只增不减，用来说「我手上是第几版」。
    /// 判断「价格变没变」看 <see cref="Fingerprint"/>：同一份内容重发一遍修订号也会涨。
    /// </summary>
    public ulong Revision { get; }

    /// <summary>内容的指纹：行的集合和每行的计价方式、价格都一样，指纹就一样。</summary>
    public string Fingerprint { get; }

    public int Count => _byKey.Count;
    public bool IsEmpty => _byKey.Count == 0;
    public IReadOnlyCollection<PublishedModelPrice> Prices => _byKey.Values;
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>键全等（归一后）。</summary>
    public PublishedModelPrice? Find(string modelKey)
        => !string.IsNullOrWhiteSpace(modelKey) && _byKey.TryGetValue(modelKey.Trim().ToLowerInvariant(), out var price) ? price : null;

    /// <summary>
    /// 上游的模型名对到表里的键：最长前缀（<see cref="ModelKeyMatcher.FindBestKey"/>）。
    /// <c>claude-3-haiku-20240307</c> 对 <c>claude-3-haiku</c>；对不上回 null。
    /// </summary>
    public PublishedModelPrice? FindBest(string vendorModel)
    {
        if (string.IsNullOrWhiteSpace(vendorModel) || _keys.Length == 0) return null;
        var key = ModelKeyMatcher.FindBestKey(vendorModel, _keys);
        return key is not null && _byKey.TryGetValue(key, out var price) ? price : null;
    }

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
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
