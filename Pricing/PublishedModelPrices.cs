using System.Text.Json;
using System.Text.Json.Serialization;
using OneApi.Common.Usage;

namespace OneApi.Common.Pricing;

/// <summary>
/// 价格表（模型的美元牌价）发布出去的样子。全站只有这一份：
/// 号池后台（VaultCustody）是它的主人，改一次就把受影响的厂家重新发布一遍；
/// 各产品的业务进程（Yomi、Jastalk……）拿到它，变了各自重算自己的有效价。
/// <para>
/// 发在 OneApi 的 NATS 库里（<see cref="OneApiKv.Bucket"/>），是其中的一张表：键都以 <see cref="Table"/> 开头，一个厂家一个键。
/// 用 NATS 而不是 Consul：改动是推给订阅的人的，断线期间漏掉的由 NATS 接着补，读的一方不用自己挂着一个请求去盯；
/// 号池后台把账号状态发给各 Provider 用的也是这个办法。
/// </para>
/// <para>
/// 订阅这张表的只有平台的 Bff 一个进程。产品不连这个 NATS（它是 OneApi 内部的总线），
/// 价格表由 Bff 原样转给它们，见 <see cref="Relay.IModelPriceRelayService"/>、<see cref="Relay.ModelPriceTableFeed"/>。
/// </para>
/// <para>
/// 表名和下面几个键都是写死的，不开放配置（为什么见 <see cref="OneApiKv"/>）。
/// </para>
/// </summary>
public static class ModelPriceKeys
{
    /// <summary>
    /// 表名：价格表在库里的键都是 <c>ratecard.</c> 开头。
    /// 叫 ratecard（价目表：一家厂商公布的标准收费表）而不是 pricing：表里放的是各厂家的标准牌价，
    /// 不是我们自己怎么定价——那是各产品拿它乘倍率以后的事。号池后台的接口、平台管理端的地址用的也是这个词。
    /// </summary>
    public const string Table = "ratecard";

    /// <summary>这张表的键的前缀。</summary>
    public const string TablePrefix = Table + ".";

    /// <summary>订阅、列出「这张表里的全部」用的过滤条件（NATS 的主题通配）。库里别的表的键不在里面。</summary>
    public const string TableFilter = Table + ".>";

    /// <summary>各厂家的键的前缀；表里不是这个前缀的键（<see cref="OwnerKey"/>）读的一方不当成价格。</summary>
    public const string MakerKeyPrefix = TablePrefix + "makers.";

    /// <summary>
    /// 记着「这份价格表是哪个库发布的」的键（这里的「库」是号池后台连的 PostgreSQL，不是 NATS 上的那个）。
    /// <para>
    /// 给发布方防呆用：价格表的主人只有一个库。另一个库的号池后台（开发机上的、连错了 NATS 的）往同一个桶里写，
    /// 会把线上各产品正在用的牌价换成它自己库里的那几行——各产品收到就会照着重算有效价、真的按它扣钱。
    /// 所以发布方每次写之前对一下这个键，对不上就不写，要人明确说「由这个库接管」才行。
    /// </para>
    /// </summary>
    public const string OwnerKey = TablePrefix + "owner";

    /// <summary>
    /// 一个厂家的键。厂家名里键不让用的字符（NATS 的键只收字母、数字和 <c>-/_=.</c>，其中点号是分层用的）
    /// 逐个写成 <c>=</c> 加四位十六进制，别的原样：<c>fireworks.ai</c> → <c>ratecard.makers.fireworks=002eai</c>。
    /// <para>
    /// 用转写而不是「换成横线」：换成横线的话 <c>a.b</c> 和 <c>a:b</c> 是同一个键，两个等长的中文厂家名也是同一个键，
    /// 两家的价格会挤进一份文档、互相盖掉。转写是一一对应的，<see cref="MakerOf"/> 能原样还原。
    /// </para>
    /// </summary>
    public static string MakerKey(string maker)
    {
        var name = NormalizeMaker(maker);
        var sb = new System.Text.StringBuilder(MakerKeyPrefix, MakerKeyPrefix.Length + name.Length);
        foreach (var c in name)
        {
            if (IsPlain(c)) sb.Append(c);
            else sb.Append('=').Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>从键里取厂家（<see cref="MakerKey"/> 的反过来）；不是厂家的键、写法对不上的回 null。</summary>
    public static string? MakerOf(string? key)
    {
        if (key is null || !key.StartsWith(MakerKeyPrefix, StringComparison.Ordinal)) return null;
        var rest = key.AsSpan(MakerKeyPrefix.Length);
        if (rest.Length == 0) return null;

        var sb = new System.Text.StringBuilder(rest.Length);
        for (var i = 0; i < rest.Length; i++)
        {
            var c = rest[i];
            if (IsPlain(c))
            {
                sb.Append(c);
                continue;
            }

            // 别的字符只能是转写：等号后面跟四位十六进制。不是这个样子的不是我们写出来的键
            if (c != '=' || i + 4 >= rest.Length
                || !int.TryParse(rest.Slice(i + 1, 4), System.Globalization.NumberStyles.AllowHexSpecifier,
                    System.Globalization.CultureInfo.InvariantCulture, out var code))
                return null;
            sb.Append((char)code);
            i += 4;
        }

        // 只认我们自己会写出来的那一种写法。一个名字能解出来、但不是它该有的键（把本来能直接写的字符也转写了、十六进制用了大写）
        // 的话，同一个厂家就有了两个键：读的一方两份都收、后到的盖掉先到的，发布方对账时又只认得、只删得掉正规的那一个
        var maker = sb.ToString();
        return string.Equals(MakerKey(maker), key, StringComparison.Ordinal) ? maker : null;
    }

    /// <summary>
    /// 厂家名的规范写法：去掉首尾空白、转小写；没有厂家的行归到 <see cref="MakerCatalog.Other"/>。
    /// 号池后台存进库里的、文档里 <c>maker</c> 写的都是它，人看得懂；键里那一段是它转写出来的（<see cref="MakerKey"/>）。
    /// </summary>
    public static string NormalizeMaker(string? maker)
    {
        var m = maker?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(m) ? MakerCatalog.Other : m;
    }

    private static bool IsPlain(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';
}

/// <summary>一个厂家的全部牌价，就是桶里一个键的值。</summary>
public sealed class PublishedMakerPrices
{
    [JsonPropertyName("maker")]
    public string Maker { get; set; } = MakerCatalog.Other;

    /// <summary>牌价的币种，恒为美元（<see cref="PriceFx.CatalogCurrency"/>）。写出来是为了读的人不用猜。</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = PriceFx.CatalogCurrency;

    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAtUtc { get; set; }

    [JsonPropertyName("prices")]
    public List<PublishedModelPrice> Prices { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, Json);

    public static PublishedMakerPrices? FromBytes(ReadOnlySpan<byte> bytes)
        => JsonSerializer.Deserialize<PublishedMakerPrices>(bytes, Json);
}

/// <summary>价格表里的一行：一个模型的美元牌价。</summary>
public sealed class PublishedModelPrice
{
    /// <summary>归一的模型键（小写、厂家自己的干净名，不带渠道前缀和日期钉）。各家用 <see cref="ModelKeyMatcher"/> 把自己的模型名对上来。</summary>
    [JsonPropertyName("modelKey")]
    public string ModelKey { get; set; } = string.Empty;

    /// <summary><see cref="BillingTypes"/> 里的一种。</summary>
    [JsonPropertyName("billingType")]
    public string BillingType { get; set; } = BillingTypes.PerToken;

    /// <summary>
    /// 价格本身，形状随 <see cref="BillingType"/>（见 <see cref="RateJsonValidator"/>）：按 token 的是每百万 token 多少美元，
    /// 分输入、输出、缓存读写；按张、按秒、按次的各有各的字段。
    /// </summary>
    [JsonPropertyName("rate")]
    public JsonElement Rate { get; set; }

    /// <summary>官方目录带来的能力（audio / reasoning / embedding）；手工录的行没有。</summary>
    [JsonPropertyName("capabilities")]
    public JsonElement? Capabilities { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>所属厂家。不在行里重复存，读的时候由所在的键补上。</summary>
    [JsonIgnore]
    public string Maker { get; set; } = MakerCatalog.Other;

    /// <summary><see cref="Rate"/> 的原文，给 <see cref="RateJsonScaler"/>、<see cref="PriceFx"/> 这些按文本处理的用。</summary>
    [JsonIgnore]
    public string RateJson => Rate.ValueKind == JsonValueKind.Undefined ? "{}" : Rate.GetRawText();
}
