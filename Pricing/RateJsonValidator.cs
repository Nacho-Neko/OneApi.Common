using System.Text.Json;
using OneApi.Common.Usage;

namespace OneApi.Common.Pricing;

/// <summary>
/// rate_json 与 billing_type 的形状校验（对齐 console rate.ts 的 zod 变体）。
/// 以前后端不校验，一条 per_image 行带着 per_token 的 body 会被 RatioResolver 静默算成 0 元。
/// 只校必需的价格字段存在且为非负数；可选字段（cachedRead / reasoning …）出现时同样要求非负数。
/// </summary>
public static class RateJsonValidator
{
    /// <returns>null = 合法；否则为可直接回给管理员的错误说明。</returns>
    public static string? Validate(string billingType, string rateJson)
    {
        if (string.IsNullOrWhiteSpace(rateJson))
            return "rate body is required.";

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rateJson);
        }
        catch (JsonException ex)
        {
            return $"rate body is not valid JSON: {ex.Message}";
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return "rate body must be a JSON object.";

            return billingType switch
            {
                BillingTypes.PerToken       => ValidatePerToken(root),
                BillingTypes.PerCall        => RequireNumber(root, "pricePerCall") ?? OptionalNumber(root, "cachedPricePerCall"),
                BillingTypes.PerImage       => ValidateTiers(root, "pricePerImage"),
                BillingTypes.PerVideo       => ValidateTiers(root, "pricePerSecond"),
                BillingTypes.PerAudioMinute => RequireNumber(root, "pricePerMinute"),
                BillingTypes.PerCharacter   => RequireNumber(root, "pricePerKChar"),
                BillingTypes.PerCredit       => RequireNumber(root, "pricePerCredit"),
                _                           => $"unsupported billing_type '{billingType}'.",
            };
        }
    }

    private static string? ValidatePerToken(JsonElement root)
    {
        if (!root.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object)
            return "per_token rate requires an 'input' object.";
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Object)
            return "per_token rate requires an 'output' object.";

        // 模态单价（audio / image）是产出侧的桶，写在 output 段；input 段的同名键是历史写法，仍认。
        return RequireNumber(input, "perMToken", "input.")
            ?? OptionalNumber(input, "cachedRead", "input.")
            ?? OptionalNumber(input, "cachedWrite", "input.")
            ?? OptionalNumber(input, "cachedWrite1h", "input.")
            ?? OptionalNumber(input, "audio", "input.")
            ?? OptionalNumber(input, "image", "input.")
            ?? RequireNumber(output, "perMToken", "output.")
            ?? OptionalNumber(output, "reasoning", "output.")
            ?? OptionalNumber(output, "audio", "output.")
            ?? OptionalNumber(output, "image", "output.");
    }

    private static string? ValidateTiers(JsonElement root, string priceKey)
    {
        if (!root.TryGetProperty("tiers", out var tiers) || tiers.ValueKind != JsonValueKind.Array)
            return "rate requires a 'tiers' array.";
        var index = 0;
        foreach (var tier in tiers.EnumerateArray())
        {
            if (tier.ValueKind != JsonValueKind.Object)
                return $"tiers[{index}] must be an object.";
            var err = RequireNumber(tier, priceKey, $"tiers[{index}].");
            if (err is not null) return err;
            index++;
        }
        return index == 0 ? "rate requires at least one tier." : null;
    }

    private static string? RequireNumber(JsonElement obj, string key, string prefix = "")
    {
        if (!obj.TryGetProperty(key, out var el))
            return $"'{prefix}{key}' is required.";
        return CheckNonNegative(el, prefix + key);
    }

    private static string? OptionalNumber(JsonElement obj, string key, string prefix = "")
    {
        if (!obj.TryGetProperty(key, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;
        return CheckNonNegative(el, prefix + key);
    }

    private static string? CheckNonNegative(JsonElement el, string path)
    {
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetDecimal(out var d))
            return $"'{path}' must be a number.";
        return d < 0 ? $"'{path}' must be non-negative." : null;
    }
}
