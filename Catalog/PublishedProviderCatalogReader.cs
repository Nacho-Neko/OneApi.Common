using System.Text.Json;
using System.Text.Json.Serialization;
using Consul;

namespace OneApi.Common.Catalog;

/// <summary>
/// 读取 OneApi <c>ProviderCatalogPublisher</c> 写在 Consul KV 的供应商模型列表。
/// Key 形状是 <c>{kvPrefix}/providers/{providerId}/catalog</c>，
/// providerId 取路径段，模型名取 payload 的 <c>models</c>。
/// 产品接入 OneApi 目录时走这里，不要再各写一遍 List / 过滤 / 反序列化。
/// </summary>
public static class PublishedProviderCatalogReader
{
    public const string KeySuffix = "/catalog";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <paramref name="kvPrefix"/> 为空时前缀是 <c>providers/</c>，否则是 <c>{kvPrefix}/providers/</c>。
    /// </summary>
    public static string ListPrefix(string? kvPrefix)
    {
        var prefix = kvPrefix?.Trim().Trim('/') ?? "";
        return string.IsNullOrEmpty(prefix) ? "providers/" : $"{prefix}/providers/";
    }

    public static Task<IReadOnlyList<PublishedProviderModels>> ListAsync(
        IConsulClient consul,
        string? kvPrefix,
        CancellationToken cancellationToken = default)
        => ListAsync(consul, kvPrefix, onMalformed: null, cancellationToken);

    public static async Task<IReadOnlyList<PublishedProviderModels>> ListAsync(
        IConsulClient consul,
        string? kvPrefix,
        Action<string, Exception>? onMalformed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consul);

        var listed = await consul.KV.List(ListPrefix(kvPrefix), cancellationToken);
        if (listed.Response is null)
            return [];

        var entries = new List<(string Key, byte[] Value)>(listed.Response.Length);
        foreach (var pair in listed.Response)
        {
            if (pair?.Key is null || pair.Value is null || pair.Value.Length == 0)
                continue;
            entries.Add((pair.Key, pair.Value));
        }

        return Read(kvPrefix, entries, onMalformed);
    }

    public static IReadOnlyList<PublishedProviderModels> Read(
        string? kvPrefix,
        IEnumerable<(string Key, byte[] Value)> entries,
        Action<string, Exception>? onMalformed = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var listPrefix = ListPrefix(kvPrefix);
        var providers = new List<PublishedProviderModels>();

        foreach (var (key, value) in entries)
        {
            if (string.IsNullOrEmpty(key) || value is null || value.Length == 0)
                continue;
            if (!key.StartsWith(listPrefix, StringComparison.Ordinal))
                continue;
            if (!key.EndsWith(KeySuffix, StringComparison.Ordinal))
                continue;

            var providerId = key[listPrefix.Length..^KeySuffix.Length];
            if (string.IsNullOrWhiteSpace(providerId) || providerId.Contains('/'))
                continue;

            CatalogWire? wire;
            try
            {
                wire = JsonSerializer.Deserialize<CatalogWire>(value, Json);
            }
            catch (JsonException ex)
            {
                onMalformed?.Invoke(key, ex);
                continue;
            }

            if (wire?.Models is null || wire.Models.Count == 0)
                continue;

            var models = wire.Models
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (models.Length == 0)
                continue;

            providers.Add(new PublishedProviderModels(providerId, models));
        }

        return providers;
    }

    private sealed class CatalogWire
    {
        [JsonPropertyName("models")]
        public List<string>? Models { get; set; }
    }
}

/// <summary>一个已发布供应商目录里的模型名。Id 是 Consul key 上的 providerId，不是 payload 字段。</summary>
public sealed record PublishedProviderModels(string ProviderId, IReadOnlyList<string> Models);
