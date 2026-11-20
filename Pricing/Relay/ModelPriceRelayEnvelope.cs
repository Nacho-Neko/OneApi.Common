using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneApi.Common.Pricing.Relay;

/// <summary>
/// 平台转给产品的整张价格表：NATS 上价格表这张表里现有的每个键和它的值，原样带过来。
/// <para>
/// 平台不解析这些文档（它不引用 OneApi.Common，也不该懂价格的形状），所以值是原始字节；
/// 哪些键是厂家、文档怎么读，是收的一方（<see cref="ModelPriceTableFeed"/>）的事。
/// </para>
/// <para>
/// 线上的样子是 gzip 过的 JSON：<c>{"fingerprint":"…","documents":{"ratecard.makers.anthropic":"&lt;base64&gt;",…}}</c>。
/// 压一下是因为一次 RPC 的消息默认只收 4 MB，而一个厂家的文档就可以到 1 MiB；价格文档是重复很多的 JSON，压完小一个量级。
/// <b>平台仓库的 Bff 里有同样的一份打包代码</b>，两边的字段名和压缩方式要一致。
/// </para>
/// </summary>
public sealed class ModelPriceRelayEnvelope
{
    /// <summary>解压以后最多认这么大。正常的表远到不了，防的是一份坏数据把进程的内存吃光。</summary>
    public const int MaxUnpackedBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>这一份的指纹，由平台算（<see cref="ComputeFingerprint"/>）。产品不用懂它，下次原样带回去问「还是这一份吗」。</summary>
    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>表里的键 → 值的原始字节（JSON 里是 base64）。</summary>
    [JsonPropertyName("documents")]
    public Dictionary<string, byte[]> Documents { get; set; } = new(StringComparer.Ordinal);

    public static ModelPriceRelayEnvelope Create(IReadOnlyDictionary<string, byte[]> documents) => new()
    {
        Fingerprint = ComputeFingerprint(documents),
        Documents = new Dictionary<string, byte[]>(documents, StringComparer.Ordinal),
    };

    public byte[] Pack()
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            JsonSerializer.Serialize(gzip, this, Json);
        return buffer.ToArray();
    }

    /// <summary>读不出来就抛 <see cref="FormatException"/>：收的一方留着手上那一份。</summary>
    public static ModelPriceRelayEnvelope Unpack(ReadOnlySpan<byte> packed)
    {
        try
        {
            using var source = new MemoryStream(packed.ToArray());
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var plain = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = gzip.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (plain.Length + read > MaxUnpackedBytes)
                    throw new FormatException($"model price relay envelope exceeds {MaxUnpackedBytes} bytes when unpacked");
                plain.Write(chunk, 0, read);
            }

            var envelope = JsonSerializer.Deserialize<ModelPriceRelayEnvelope>(plain.GetBuffer().AsSpan(0, (int)plain.Length), Json)
                           ?? throw new FormatException("model price relay envelope is null");
            // 反序列化出来的字典是默认比较器，统一成按序号比的
            envelope.Documents = new Dictionary<string, byte[]>(envelope.Documents ?? [], StringComparer.Ordinal);
            envelope.Fingerprint ??= string.Empty;
            return envelope;
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            throw new FormatException("model price relay envelope is malformed", ex);
        }
    }

    /// <summary>
    /// 内容的指纹：键按序号排，逐个把「键、分隔、值的长度、值」喂进 SHA-256。
    /// 按内容算而不是按 NATS 的修订号：平台有几个副本、各自什么时候连上的，对同一份内容算出来都一样。
    /// </summary>
    public static string ComputeFingerprint(IReadOnlyDictionary<string, byte[]> documents)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[8];
        foreach (var (key, value) in documents.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(key));
            hash.AppendData([0]);
            BinaryPrimitives.WriteInt64BigEndian(length, value.LongLength);
            hash.AppendData(length);
            hash.AppendData(value);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
