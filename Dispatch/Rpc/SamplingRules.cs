using MessagePack;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// 入口下发的采样策略。网关在渠道协议上套用：温度等参数、思考档位、提示词、缓存断点。
/// 回包内容过滤留在入口。
///
/// <para>Key 是数组下标。加字段只能追加到下一个空位。</para>
/// </summary>
[MessagePackObject]
public sealed class SamplingRules
{
    [Key(0)] public SamplingParam? Temperature { get; set; }
    [Key(1)] public SamplingParam? TopP { get; set; }
    [Key(2)] public SamplingParam? TopK { get; set; }
    [Key(3)] public SamplingParam? MaxTokens { get; set; }
    [Key(4)] public SamplingParam? FrequencyPenalty { get; set; }
    [Key(5)] public SamplingParam? PresencePenalty { get; set; }
    [Key(6)] public SamplingChoice? ThinkingLevel { get; set; }
    [Key(7)] public List<SamplingPrompt>? Prompts { get; set; }
    [Key(8)] public SamplingPromptCache? PromptCache { get; set; }
}

/// <summary>一个数值采样参数。Unset 不出现在契约上，调用方留 null。</summary>
[MessagePackObject]
public sealed class SamplingParam
{
    [Key(0)] public SamplingMerge Mode { get; set; }
    [Key(1)] public double? Value { get; set; }
}

/// <summary>思考档位。Effort 只在 Fallback / Override 下有值。</summary>
[MessagePackObject]
public sealed class SamplingChoice
{
    [Key(0)] public SamplingMerge Mode { get; set; }
    [Key(1)] public SamplingEffort? Effort { get; set; }
}

/// <summary>
/// 一条要插入的提示词。下标按列表顺序，禁用条和分隔条也要带着，
/// 同 order 时的先后才和库存一致。
/// </summary>
[MessagePackObject]
public sealed class SamplingPrompt
{
    [Key(0)] public string? Content { get; set; }
    [Key(1)] public bool Enabled { get; set; } = true;
    [Key(2)] public string? Role { get; set; }
    [Key(3)] public int InjectionPosition { get; set; }
    [Key(4)] public int InjectionDepth { get; set; } = 4;
    [Key(5)] public int InjectionOrder { get; set; } = 100;
    [Key(6)] public bool Separator { get; set; }
}

/// <summary>Anthropic <c>cache_control</c>。其它渠道网关忽略。</summary>
[MessagePackObject]
public sealed class SamplingPromptCache
{
    [Key(0)] public SamplingMerge Mode { get; set; }
    [Key(1)] public SamplingCacheTarget? System { get; set; }
    [Key(2)] public SamplingCacheTarget? Tools { get; set; }
    [Key(3)] public SamplingCacheTarget? Messages { get; set; }
}

/// <summary>一个缓存断点。Depth 为空表示打在该段最后一条。</summary>
[MessagePackObject]
public sealed class SamplingCacheTarget
{
    [Key(0)] public bool Enabled { get; set; } = true;
    [Key(1)] public int? Depth { get; set; }
}

/// <summary>和库存里的合并模式同号。改号等于改线上契约。</summary>
public enum SamplingMerge
{
    Unset = 0,
    Fallback = 1,
    Override = 2,
    Suppress = 3,
}

/// <summary>和思考档位阶梯同号。不认识的值网关当成没配。</summary>
public enum SamplingEffort
{
    Off = 0,
    Minimal = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    XHigh = 5,
    Max = 6,
}
