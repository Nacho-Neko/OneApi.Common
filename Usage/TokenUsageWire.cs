namespace OneApi.Common.Usage;

/// <summary>
/// 公共 <see cref="TokenUsage"/>（input/output）与历史 wire 命名（prompt/completion）之间的桥接。
/// DemuxAi <c>TokenUsageBreakdown</c>、Tavern <c>TavernModelUsage.PromptTokens</c> 等
/// MessagePack 契约保留旧字段名，业务层经此转换后再走 <see cref="UsageJsonCodec"/>。
/// </summary>
public static class TokenUsageWire
{
    public static TokenUsage FromBreakdown(
        int prompt,
        int completion,
        int reasoning = 0,
        int cachedRead = 0,
        int inputAudio = 0) =>
        new(
            InputTokens: Math.Max(0, prompt),
            OutputTokens: Math.Max(0, completion),
            ReasoningTokens: Math.Max(0, reasoning),
            CachedReadTokens: Math.Max(0, cachedRead),
            InputAudioTokens: Math.Max(0, inputAudio));

    public static (int PromptTokens, int CompletionTokens) ToPromptCompletion(TokenUsage usage) =>
        (usage.InputTokens, usage.OutputTokens);

    /// <summary>
    /// token 类用量 → Tavern commit 所需的 (kind, unit, prompt, completion)。
    /// <paramref name="usageKind"/> 为落库 kind（<see cref="UsageKinds"/>），经 <see cref="UsageKinds.ToTavernCommitKind"/> 折叠。
    /// </summary>
    public static (string CommitKind, string Unit, int PromptTokens, int CompletionTokens) ToTavernTokenCommit(
        string usageKind,
        TokenUsage usage) =>
        (
            UsageKinds.ToTavernCommitKind(usageKind),
            UsageUnits.ForKind(usageKind),
            usage.InputTokens,
            usage.OutputTokens);

    /// <summary>媒体类用量 → Tavern commit 所需的 (kind, unit, quantity)。</summary>
    public static (string CommitKind, string Unit, decimal Quantity) ToTavernMediaCommit(
        string usageKind,
        decimal quantity) =>
        (
            UsageKinds.ToTavernCommitKind(usageKind),
            UsageUnits.ForKind(usageKind),
            quantity);
}
