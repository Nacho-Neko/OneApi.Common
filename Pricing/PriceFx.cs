namespace OneApi.Common.Pricing;

/// <summary>
/// 价格表是美元；有效价是人民币。
/// 编译：美元基准 × 渠道倍率 × 路由倍率。倍率本身就是换算，不再另乘 7。
/// 遗留 CNY 牌价先 ÷ <see cref="UsdCny"/> 收成美元再乘倍率。
/// </summary>
public static class PriceFx
{
    /// <summary>有效价、账单、rate_snapshots。</summary>
    public const string Currency = "CNY";

    /// <summary>价格表 / model_prices。</summary>
    public const string CatalogCurrency = "USD";

    public const decimal UsdCny = 7m;

    /// <summary>
    /// 把源币种折成人民币要乘的倍数。只给仍要看「牌价对人民币」的读路径；
    /// 编译走 <see cref="ToEffective"/>，用调用方给的渠道倍率。
    /// </summary>
    public static decimal FactorToCny(string? catalogCurrency)
    {
        var cur = (catalogCurrency ?? CatalogCurrency).Trim().ToUpperInvariant();
        if (cur.Length == 0 || string.Equals(cur, CatalogCurrency, StringComparison.Ordinal))
            return UsdCny;
        if (string.Equals(cur, Currency, StringComparison.Ordinal))
            return 1m;
        throw new ArgumentException(
            $"unsupported catalog currency '{catalogCurrency}'; only USD and CNY are accepted.",
            nameof(catalogCurrency));
    }

    /// <summary>有效价：牌价先收成美元，再 × <paramref name="multiplier"/>（渠道 / 路由倍率），结果是人民币。</summary>
    public static string ToEffective(string catalogJson, string? catalogCurrency, decimal multiplier = 1m)
    {
        if (multiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "multiplier must be non-negative.");
        var (usd, _) = ToUsd(catalogJson, catalogCurrency);
        return multiplier == 1m ? usd : RateJsonScaler.Scale(usd, multiplier);
    }

    /// <summary>
    /// 源报价收到价格表：USD 原样；CNY 源 ÷7 收成美元。别的币种不猜。
    /// </summary>
    public static (string RateJson, decimal Applied) ToUsd(string rateJson, string? sourceCurrency)
    {
        var cur = (sourceCurrency ?? CatalogCurrency).Trim().ToUpperInvariant();
        if (cur.Length == 0 || string.Equals(cur, CatalogCurrency, StringComparison.Ordinal))
            return (rateJson, 1m);
        if (string.Equals(cur, Currency, StringComparison.Ordinal))
            return (RateJsonScaler.Scale(rateJson, 1m / UsdCny), 1m / UsdCny);
        throw new ArgumentException(
            $"unsupported source currency '{sourceCurrency}'; only CNY and USD are accepted.",
            nameof(sourceCurrency));
    }

    /// <summary>把源币种的 rate_json 折成人民币（读路径；编译不走这里）。</summary>
    public static (string RateJson, decimal Applied) ToCny(string rateJson, string? sourceCurrency)
    {
        var factor = FactorToCny(sourceCurrency);
        return factor == 1m ? (rateJson, 1m) : (RateJsonScaler.Scale(rateJson, factor), factor);
    }
}
