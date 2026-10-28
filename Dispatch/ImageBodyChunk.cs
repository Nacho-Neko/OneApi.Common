using MessagePack;

namespace OneApi.Common.Dispatch;

/// <summary>
/// 生图正文的一块。只在生图这条流上出现，和对话用的 <see cref="StreamingChunkDto"/> 不是同一个数组。
/// </summary>
[MessagePackObject]
public sealed class ImageBodyChunk
{
    /// <summary>从 0 递增。接收端按这个顺序写盘，缺号就失败。</summary>
    [Key(0)] public int Sequence { get; set; }

    /// <summary>这一块正文。结束帧和失败帧不带。</summary>
    [Key(1)] public byte[]? Bytes { get; set; }

    /// <summary>流结束。成功时是最后一帧，失败时和 <see cref="Error"/> 一起出现。</summary>
    [Key(2)] public bool Done { get; set; }

    [Key(3)] public string? Error { get; set; }

    [Key(4)] public string? ErrorCode { get; set; }

    [Key(5)] public Dictionary<string, string>? ErrorParams { get; set; }

    /// <summary>
    /// 这次生成的用量（键见 <see cref="Usage.CreditUsageKeys"/>）。
    /// 上游不回扣费数据，Provider 在发车前就按参数算好了，所以附在<b>第一块正文</b>上，
    /// 而不是结束帧——结束帧在网关里被吞掉，接收端收不到。接收端收到任一块带它就记下，
    /// 后到的覆盖先到的；没带就是旧 Provider，接收端按自己的估算走。
    /// </summary>
    [Key(6)] public Dictionary<string, long>? Usage { get; set; }
}
