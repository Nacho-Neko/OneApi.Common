using System.Buffers;
using System.Text;
using System.Text.Json;

namespace OneApi.Common.Pricing;

/// <summary>
/// 对 rate_json 里的价格数字统一乘一个因子，结构原样保留。
/// 各 billingType 的价格字段名各不相同（perMToken / pricePerCall / pricePerImage / pricePerSecond / pricePerCredit …），
/// 与其维护一张白名单不如反过来：除了明确不是价格的数字字段，其余数字都是价格。
/// </summary>
public static class RateJsonScaler
{
    /// <summary>rate_json 中出现的非价格数字字段：视频计费的时长上下限。</summary>
    private static readonly HashSet<string> NonPriceNumericKeys = new(StringComparer.Ordinal)
    {
        "minSeconds", "maxSeconds",
    };

    private const int PriceScale = 6;

    public static string Scale(string rateJson, decimal factor)
    {
        if (factor < 0)
            throw new ArgumentOutOfRangeException(nameof(factor), "scale factor must be non-negative.");

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rateJson) ? "{}" : rateJson);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteScaled(doc.RootElement, writer, factor, propertyName: null);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteScaled(JsonElement el, Utf8JsonWriter writer, decimal factor, string? propertyName)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in el.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    WriteScaled(prop.Value, writer, factor, prop.Name);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in el.EnumerateArray())
                    WriteScaled(item, writer, factor, propertyName);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                if (propertyName is not null && NonPriceNumericKeys.Contains(propertyName))
                {
                    el.WriteTo(writer);
                }
                else
                {
                    var scaled = decimal.Round(el.GetDecimal() * factor, PriceScale, MidpointRounding.AwayFromZero);
                    writer.WriteNumberValue(Normalize(scaled));
                }
                break;
            default:
                el.WriteTo(writer);
                break;
        }
    }

    /// <summary>
    /// 数值等价比较：结构相同且每个数字相等（12.5 与 12.50 视为相等），字符串按序数比较。
    /// 用来判断重编译是否真的改了价，避免每次都 supersede 出一堆内容相同的新行。
    /// </summary>
    public static bool NumericallyEqual(string leftJson, string rightJson)
    {
        try
        {
            using var l = JsonDocument.Parse(string.IsNullOrWhiteSpace(leftJson) ? "{}" : leftJson);
            using var r = JsonDocument.Parse(string.IsNullOrWhiteSpace(rightJson) ? "{}" : rightJson);
            return Equal(l.RootElement, r.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Equal(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var bProps = b.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                var count = 0;
                foreach (var p in a.EnumerateObject())
                {
                    count++;
                    if (!bProps.TryGetValue(p.Name, out var bv) || !Equal(p.Value, bv))
                        return false;
                }
                return count == bProps.Count;
            }
            case JsonValueKind.Array:
            {
                var aItems = a.EnumerateArray().ToArray();
                var bItems = b.EnumerateArray().ToArray();
                if (aItems.Length != bItems.Length) return false;
                for (var i = 0; i < aItems.Length; i++)
                    if (!Equal(aItems[i], bItems[i])) return false;
                return true;
            }
            case JsonValueKind.Number:
                return a.TryGetDecimal(out var ad) && b.TryGetDecimal(out var bd) && ad == bd;
            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            default:
                return true;
        }
    }

    /// <summary>去掉 decimal 的尾随零，避免 12.500000 这种写法进 jsonb。</summary>
    private static decimal Normalize(decimal value) => value / 1.000000000000000000000000000000000m;
}
