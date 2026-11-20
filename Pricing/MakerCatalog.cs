namespace OneApi.Common.Pricing;

/// <summary>
/// 模型厂家（maker）：模型是谁做的，与渠道 / queueGroup 无关。
/// 规则对齐 console <c>maker.ts</c>：键是开放集合，认识的几家有排序权重；猜不到落 <see cref="Other"/>。
/// 判定优先按模型键——阿里云转售的 glm / kimi 仍归智谱 / Moonshot，不跟源的 provider 走。
/// </summary>
public static class MakerCatalog
{
    public const string Other = "other";

    private sealed record MakerInfo(string Key, int Order, string[] Patterns);

    /// <summary>与 console KNOWN_MAKERS 同序：先命中的赢。权重只用于分组排序。</summary>
    private static readonly MakerInfo[] Known =
    [
        new("openai",    10,  ["gpt-", "gpt4", "chatgpt", "o1", "o3", "o4", "text-embedding-3", "text-embedding-ada", "dall-e", "whisper", "tts-", "codex", "sora"]),
        new("anthropic", 20,  ["claude"]),
        new("google",    30,  ["gemini", "gemma", "imagen", "veo", "palm"]),
        new("deepseek",  40,  ["deepseek"]),
        new("qwen",      50,  ["qwen", "qwq", "qvq", "text-embedding-v", "tongyi-embedding"]),
        new("moonshot",  60,  ["kimi", "moonshot"]),
        new("zhipu",     70,  ["glm", "chatglm", "cogview", "cogvideo"]),
        new("xai",       80,  ["grok"]),
        new("meta",      90,  ["llama"]),
        new("mistral",   100, ["mistral", "mixtral", "codestral", "pixtral", "ministral"]),
        new("minimax",   110, ["minimax", "abab"]),
        new("doubao",    120, ["doubao"]),
        new("baidu",     130, ["ernie"]),
        new("tencent",   140, ["hunyuan"]),
        new("stepfun",   150, ["step-"]),
        new("cohere",    160, ["command-", "embed-", "rerank-"]),
        new("ai21",      161, ["jamba"]),
        new("perplexity",162, ["sonar"]),
        new("upstage",   163, ["solar-"]),
        new("inception", 164, ["mercury"]),
        new("bfl",       170, ["flux"]),
        new("novelai",   175, ["nai-diffusion"]),
        new("midjourney",180, ["midjourney", "mj-"]),
    ];

    private static readonly Dictionary<string, MakerInfo> ByKey =
        Known.ToDictionary(m => m.Key, StringComparer.Ordinal);

    /// <summary>源偶尔用产品名当厂家，对回我们的 key。</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["claude"]     = "anthropic",
        ["gemini"]     = "google",
        ["kimi"]       = "moonshot",
        ["moonshotai"] = "moonshot",
        ["hunyuan"]    = "tencent",
        ["flux"]       = "bfl",
        ["llama"]      = "meta",
        ["grok"]       = "xai",
        ["x-ai"]       = "xai",
        ["zhipuai"]    = "zhipu",
        ["zai"]        = "zhipu",
        ["alibaba"]    = "qwen",
        ["alibaba-cn"] = "qwen",
    };

    public static string Normalize(string? raw)
    {
        var k = raw?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(k) || k == Other) return Other;
        if (Aliases.TryGetValue(k, out var aliased)) return aliased;
        return k;
    }

    /// <summary>认识的厂家键，或能对回厂家键的别名。</summary>
    public static bool IsKnown(string? raw)
    {
        var n = Normalize(raw);
        return n != Other && ByKey.ContainsKey(n);
    }

    /// <summary>
    /// 源把厂家写进模型 id（<c>anthropic.claude-sonnet-4-5</c>）时用来剥前缀。
    /// 含厂家键和别名（<c>claude</c> / <c>moonshotai</c>）。
    /// </summary>
    public static IEnumerable<string> KnownTokens()
    {
        foreach (var k in ByKey.Keys) yield return k;
        foreach (var k in Aliases.Keys) yield return k;
    }

    /// <summary>models.dev 的 provider 就是厂家本人，不是转售渠道。</summary>
    public static bool IsFirstParty(string? sourceProvider, string maker)
        => IsKnown(sourceProvider) && Normalize(sourceProvider) == Normalize(maker);

    /// <summary>
    /// 厂家自己的模型目录（<c>anthropic/claude-opus-5</c>），不是 Vertex / Bedrock / 网关转售。
    /// </summary>
    public static bool IsOfficialCatalog(string? providerId)
    {
        if (!IsKnown(providerId)) return false;
        var raw = providerId!.Trim().ToLowerInvariant();
        return raw.IndexOf("vertex", StringComparison.Ordinal) < 0
            && raw.IndexOf("bedrock", StringComparison.Ordinal) < 0
            && raw.IndexOf("azure", StringComparison.Ordinal) < 0
            && raw.IndexOf("gateway", StringComparison.Ordinal) < 0
            && raw.IndexOf("openrouter", StringComparison.Ordinal) < 0
            && raw.IndexOf("-plan", StringComparison.Ordinal) < 0
            && !raw.EndsWith("-cn", StringComparison.Ordinal);
    }

    /// <summary>写入 / 下发：空则按键推断，否则只做归一（小写、别名）。</summary>
    public static string NormalizeOrInfer(string? raw, string modelKey)
    {
        var k = raw?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(k) ? Infer(modelKey) : Normalize(k);
    }

    /// <summary>
    /// 候选报价的厂家：先按模型键猜（转售渠道不算厂家），猜不到再用源给出的 provider。
    /// </summary>
    public static string Resolve(string? sourceProvider, string modelKey)
    {
        var fromKey = Infer(modelKey);
        if (fromKey != Other) return fromKey;
        var fromSource = Normalize(sourceProvider);
        return fromSource;
    }

    public static string Infer(string modelKey)
    {
        var k = modelKey.Trim().ToLowerInvariant();
        if (k.Length == 0) return Other;

        var slash = k.IndexOf('/');
        if (slash > 0)
        {
            var head = k[..slash];
            if (ByKey.ContainsKey(head)) return head;
            if (Aliases.TryGetValue(head, out var aliased)) return aliased;
        }

        foreach (var m in Known)
        {
            foreach (var p in m.Patterns)
            {
                if (k.StartsWith(p, StringComparison.Ordinal)
                    || k.Contains("-" + p, StringComparison.Ordinal)
                    || k.Contains("/" + p, StringComparison.Ordinal)
                    || k.Contains("_" + p, StringComparison.Ordinal))
                    return m.Key;
            }
        }

        return Other;
    }

    public static int Order(string key)
    {
        if (key == Other) return int.MaxValue;
        return ByKey.TryGetValue(key, out var m) ? m.Order : 1000;
    }

    public static int Compare(string a, string b)
    {
        var d = Order(a) - Order(b);
        return d != 0 ? d : string.CompareOrdinal(a, b);
    }
}
