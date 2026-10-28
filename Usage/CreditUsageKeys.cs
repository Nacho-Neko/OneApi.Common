using System.Globalization;
using System.Text.Json;

namespace OneApi.Common.Usage;

/// <summary>
/// 上游积分在 Provider 内部 usage 事件（<c>Dictionary&lt;string, object&gt;</c>）里的键。
/// 上线时不走 long 用量字典，而是独立的 decimal 字段：对话是
/// <see cref="Dispatch.StreamingChunkDto.Credits"/>，生图是 <see cref="Dispatch.ImageBodyChunk.Credits"/>。
/// 「积分」是上游厂商自己的计量单位（NovelAI 叫 Anlas），各家各算各的，常是小数，网关只认这个数。
/// </summary>
public static class CreditUsageKeys
{
    /// <summary>
    /// 这次调用实际用掉的积分。Kiro 由上游流里的 meteringEvent 给；NovelAI 上游不回，
    /// Provider 按号池账号的档位和请求参数算出账号实际被扣的数（<see cref="NovelAiAnlas"/>）。
    /// 网关不估、不改，原样交给控制面按每积分单价扣钱。
    /// </summary>
    public const string Credits = "credits";

    /// <summary>usage 事件里的值可能是 double / decimal / long / JsonElement；读不出数或为负返回 null。</summary>
    public static decimal? TryRead(object? value)
    {
        decimal? d = value switch
        {
            decimal m => m,
            double x when !double.IsNaN(x) && !double.IsInfinity(x) => (decimal)x,
            float f when !float.IsNaN(f) && !float.IsInfinity(f) => (decimal)f,
            long l => l,
            int i => i,
            JsonElement { ValueKind: JsonValueKind.Number } je when je.TryGetDecimal(out var jd) => jd,
            string s when decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var sd) => sd,
            _ => null,
        };
        return d is >= 0m ? d : null;
    }
}
