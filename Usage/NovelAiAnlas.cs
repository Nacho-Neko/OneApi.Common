using System.Globalization;
using System.Text.Json;

namespace OneApi.Common.Usage;

/// <summary>
/// 按 NovelAI generate-image 请求参数本地估算一次生成要扣的 Anlas。
///
/// <para>为什么要估：<c>POST /ai/generate-image</c> 只回一个 PNG 的 ZIP，正文和响应头都没有
/// 扣费信息，余额只能通过 <c>GET /user/subscription</c> 另查。每张图前后各查一次余额太重，
/// 而官方前端的计费就是一个确定性公式，拿参数本地算出来就够计费和打日志。</para>
///
/// <para>公式取自 NovelAI 前端（Aedial/novelai-api <c>ImagePreset.calculate_cost</c> 的 v3 分支），
/// 1024×1024、28 步算出 20，与官网公布的价格一致。v4 起的模型没有 SMEA，公式其余部分不变，
/// 这里对 v3 以后的所有模型都用它。</para>
///
/// <para>放在 OneApi.Common 而不是 Neko.Api：号池 Provider 用它算实扣并随回包带回，
/// Yomi.Gateway 用它在派发前估冻结额。两边必须是同一个公式，否则冻结与实扣对不上。</para>
///
/// <para>算不到的部分：v5 的用量上限用完后"每张都扣"的规则（服务端状态，本地无从得知）、
/// Vibe Transfer 编码单独收的 Anlas、以及订阅过期。所以这是估算，不是账单。</para>
/// </summary>
public static class NovelAiAnlas
{
    /// <summary>Opus 免费的尺寸上限：1024×1024。</summary>
    private const long OpusFreeMaxPixels = 1024L * 1024;

    /// <summary>Opus 免费的步数上限。</summary>
    public const int OpusFreeMaxSteps = 28;

    /// <summary>低于 256×256 一律按 256×256 计。</summary>
    private const long MinPixels = 256L * 256;

    /// <summary>每张图的最低价。</summary>
    private const int MinPerSample = 2;

    /// <summary>
    /// <paramref name="body"/> 是完整请求体（<c>input</c> / <c>model</c> / <c>action</c> /
    /// <c>parameters</c>）。缺 <c>parameters</c> 或缺宽高时返回 null，调用方整段不打这个字段。
    /// </summary>
    public static NovelAiAnlasEstimate? Estimate(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("parameters", out var p)
            || p.ValueKind != JsonValueKind.Object)
            return null;

        var width = ReadInt(p, "width");
        var height = ReadInt(p, "height");
        if (width is null or <= 0 || height is null or <= 0) return null;

        var action = ReadString(body, "action") ?? ReadString(p, "action") ?? "generate";
        var usesBaseImage = !action.Equals("generate", StringComparison.OrdinalIgnoreCase)
                            || p.TryGetProperty("image", out var img) && img.ValueKind == JsonValueKind.String;

        return Compute(
            width.Value, height.Value,
            steps: ReadInt(p, "steps") ?? OpusFreeMaxSteps,
            samples: ReadInt(p, "n_samples") ?? 1,
            smea: ReadBool(p, "sm") ?? false,
            smeaDyn: ReadBool(p, "sm_dyn") ?? false,
            usesBaseImage: usesBaseImage,
            strength: ReadDouble(p, "strength"),
            uncondScale: ReadDouble(p, "uncond_scale"));
    }

    /// <summary>
    /// 纯参数版。调用方手里没有完整 NovelAI 体（比如还在 OpenAI Images 形状、由网关转写）时用它，
    /// 未知项按 NovelAI 转写默认值传：28 步、无 SMEA、无底图。
    /// </summary>
    public static NovelAiAnlasEstimate Compute(
        int width, int height, int steps, int samples,
        bool smea = false, bool smeaDyn = false,
        bool usesBaseImage = false, double? strength = null, double? uncondScale = null)
    {
        steps = Math.Max(1, steps);
        samples = Math.Max(1, samples);
        var pixels = Math.Max(MinPixels, (long)Math.Max(1, width) * Math.Max(1, height));

        // 前端公式：固定项按像素计，步数项按像素 × 步数计，先取整再乘 SMEA 系数。
        var smeaFactor = smea && smeaDyn ? 1.4 : smea ? 1.2 : 1.0;
        var perSample = Math.Ceiling(2.951823174884865e-6 * pixels + 5.753298233447344e-7 * pixels * steps)
                        * smeaFactor;

        // img2img 按去噪强度打折；无底图时 strength 没有意义，不读。
        var effectiveStrength = usesBaseImage ? strength ?? 1.0 : 1.0;
        var rounded = Math.Max((int)Math.Ceiling(perSample * effectiveStrength), MinPerSample);

        if (uncondScale is { } scale && Math.Abs(scale - 1.0) > 1e-9)
            rounded = (int)Math.Ceiling(rounded * scale);

        // 官方口径：Opus 免费要求单张、无底图、不超过 1024×1024、不超过 28 步。
        var opusFree = samples == 1
                       && !usesBaseImage
                       && steps <= OpusFreeMaxSteps
                       && pixels <= OpusFreeMaxPixels;

        return new NovelAiAnlasEstimate(rounded, samples, opusFree);
    }

    private static int? ReadInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.Number when v.TryGetDouble(out var d) => (int)d,
            JsonValueKind.String when int.TryParse(v.GetString(), out var s) => s,
            _ => null,
        };
    }

    private static double? ReadDouble(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(
                v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
    }

    private static bool? ReadBool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>
/// 一次生成的估价。<see cref="Full"/> 是 NovelAI 的标价（非 Opus 账号会被扣的数）；
/// Opus 且满足免费条件时账号实际扣 0。
/// </summary>
/// <param name="PerSample">单张价格。</param>
/// <param name="Samples">张数。</param>
/// <param name="OpusFree">是否落在 Opus 免费范围内。</param>
public readonly record struct NovelAiAnlasEstimate(int PerSample, int Samples, bool OpusFree)
{
    public int Full => PerSample * Samples;

    /// <summary>按已知档位算号池账号的实际扣费；档位未知返回 null。</summary>
    public int? Charged(bool? isOpus) => isOpus switch
    {
        true => OpusFree ? 0 : Full,
        false => Full,
        null => null,
    };

    /// <summary>
    /// 完成日志里 <c>anlas=</c> 的值。档位已知就给实际扣费；未知时给标价，
    /// 并标注 Opus 下是否免费，让读日志的人自己对号。
    /// </summary>
    public string Describe(bool? isOpus) => isOpus switch
    {
        true when OpusFree => "0 (opus-free)",
        true or false => Full.ToString(CultureInfo.InvariantCulture),
        null when OpusFree => $"{Full} (free on opus)",
        null => Full.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// 随生图回包带回网关的用量字典（<see cref="CreditUsageKeys"/>）。
    /// 标价总是带；号池账号的实扣只在档位已知时带。
    /// </summary>
    public Dictionary<string, long> ToUsage(bool? isOpus)
    {
        var usage = new Dictionary<string, long>(2) { [CreditUsageKeys.Credits] = Full };
        if (Charged(isOpus) is { } charged)
            usage[CreditUsageKeys.CreditsCharged] = charged;
        return usage;
    }
}
