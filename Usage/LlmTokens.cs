using MessagePack;

namespace OneApi.Common.Usage;

/// <summary>
/// LLM 输入：未命中缓存的、命中缓存读取的、写入缓存的，三者互不包含。
/// <para>
/// 只有净值：上游各协议的口径（输入含不含缓存）由 <c>NativeUsageReader</c> 在读的时候换算掉，
/// 照抄一份毛值进来，就是同一批 token 按两种单价收两遍。
/// </para>
/// <para>跨服务契约（Yomi 的 <c>LlmUsage</c> / <c>EmbeddingUsage</c> 里嵌着它），Key 只能往后加。</para>
/// </summary>
[MessagePackObject]
public sealed record LlmInput : IMergeableUsage<LlmInput>
{
    /// <summary>未命中缓存的输入。</summary>
    [Key(0)] public long Prompt { get; init; }

    /// <summary>命中缓存读取的输入。</summary>
    [Key(1)] public long CachedRead { get; init; }

    /// <summary>写入（创建）缓存的输入。</summary>
    [Key(2)] public long CachedWrite { get; init; }

    /// <summary>上游 usage 是累计口径，分次报的同一项取最大。</summary>
    public LlmInput Merge(LlmInput later) => new()
    {
        Prompt      = Math.Max(Prompt, later.Prompt),
        CachedRead  = Math.Max(CachedRead, later.CachedRead),
        CachedWrite = Math.Max(CachedWrite, later.CachedWrite),
    };
}

/// <summary>
/// LLM 产出：正文、思考、图像、音频，四者互不包含（产出含不含思考 / 模态同样由读取器换算掉）。
/// 图像 / 音频是聊天模型回图 / 回音频时上游按 token 报的那一份，与生图按张数计不是一回事。
/// 视频产出没有单列，含在正文里按正文单价计。
/// <para>跨服务契约，Key 只能往后加。</para>
/// </summary>
[MessagePackObject]
public sealed record LlmOutput : IMergeableUsage<LlmOutput>
{
    /// <summary>正文。</summary>
    [Key(0)] public long Text { get; init; }

    /// <summary>思考 / reasoning。</summary>
    [Key(1)] public long Reasoning { get; init; }

    /// <summary>图像 token。</summary>
    [Key(2)] public long Image { get; init; }

    /// <summary>音频 token（单价通常远高于正文）。</summary>
    [Key(3)] public long Audio { get; init; }

    /// <summary>上游 usage 是累计口径，分次报的同一项取最大。</summary>
    public LlmOutput Merge(LlmOutput later) => new()
    {
        Text      = Math.Max(Text, later.Text),
        Reasoning = Math.Max(Reasoning, later.Reasoning),
        Image     = Math.Max(Image, later.Image),
        Audio     = Math.Max(Audio, later.Audio),
    };
}
