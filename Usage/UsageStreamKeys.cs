namespace OneApi.Common.Usage;

/// <summary>
/// Provider → Gateway 流式 usage 字典的<b>规范键名</b>（camelCase）。
/// 所有 Provider 在发出 usage 事件前必须归一到此键集；网关 / relay 读取消耗时也只认此键 +
/// <see cref="LegacyInputCandidates"/> / <see cref="LegacyOutputCandidates"/> 中的历史别名。
/// 写库前经 <see cref="ToTokenUsage"/> 折叠为 <see cref="TokenUsage"/> → <see cref="UsageJsonCodec"/>。
/// </summary>
public static class UsageStreamKeys
{
    public const string InputTokens = "inputTokens";
    public const string OutputTokens = "outputTokens";
    public const string TotalTokens = "totalTokens";
    public const string ReasoningTokens = "reasoningTokens";
    public const string CacheCreationInputTokens = "cacheCreationInputTokens";
    public const string CacheReadInputTokens = "cacheReadInputTokens";
    public const string OutputImageTokens = "outputImageTokens";
    public const string OutputAudioTokens = "outputAudioTokens";
    public const string OutputVideoTokens = "outputVideoTokens";

    /// <summary>上游 OpenAI / 旧 wire 可能使用的 input 别名。</summary>
    public static readonly string[] LegacyInputCandidates =
        [InputTokens, "input_tokens", "prompt_tokens", "promptTokens"];

    /// <summary>上游 OpenAI / 旧 wire 可能使用的 output 别名。</summary>
    public static readonly string[] LegacyOutputCandidates =
        [OutputTokens, "output_tokens", "completion_tokens", "completionTokens"];

    /// <summary>从流式 usage 字典读取 long；按 <paramref name="keys"/> 顺序尝试，缺失返回 0。</summary>
    public static long ReadLong(IReadOnlyDictionary<string, long> usage, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (usage.TryGetValue(key, out var v))
                return Math.Max(0, v);
        }
        return 0;
    }

    /// <summary>从规范（+ legacy 别名）流式 usage 字典折叠为 <see cref="TokenUsage"/>。</summary>
    public static TokenUsage ToTokenUsage(IReadOnlyDictionary<string, long> usage) => new(
        InputTokens: ToInt(ReadLong(usage, LegacyInputCandidates)),
        OutputTokens: ToInt(ReadLong(usage, LegacyOutputCandidates)),
        ReasoningTokens: ToInt(ReadLong(usage, ReasoningTokens)),
        CachedReadTokens: ToInt(ReadLong(usage, CacheReadInputTokens, "cache_read_input_tokens")),
        CachedWriteTokens: ToInt(ReadLong(usage, CacheCreationInputTokens, "cache_creation_input_tokens")),
        OutputAudioTokens: ToInt(ReadLong(usage, OutputAudioTokens)));

    private static int ToInt(long value) => (int)Math.Min(Math.Max(0, value), int.MaxValue);

    /// <summary>
    /// wire 命名（PromptTokens / CompletionTokens，DemuxAi RPC / Tavern 计费 DTO）→ 公共 <see cref="TokenUsage"/>。
    /// </summary>
    public static TokenUsage FromPromptCompletion(int promptTokens, int completionTokens, int reasoningTokens = 0) =>
        new(promptTokens, completionTokens, reasoningTokens);
}
