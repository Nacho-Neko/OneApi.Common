using MessagePack;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary><see cref="DispatchStreamFrame.FrameType"/> 取值。</summary>
public static class DispatchFrameType
{
    /// <summary>任务认领成功；携带 taskId / instanceId / 解析出的 conversationId。每流至多一帧、先于所有 Chunk。</summary>
    public const byte Claim = 0;

    /// <summary>一个透传的流式 chunk（<see cref="DispatchStreamFrame.ChunkPayload"/>）。</summary>
    public const byte Chunk = 1;

    /// <summary>派发/上游错误；总是流的最后一帧。</summary>
    public const byte Error = 2;
}

/// <summary>
/// 流式派发帧。Chunk 帧透传 MessagePack 序列化的 <c>StreamingChunkDto</c>
/// （Gateway.Shared 类型）字节，网关不做任何结构化降维——Edge 反序列化后
/// 得到与网关本地派发完全一致的 chunk 序列（字节级保真）。
/// </summary>
[MessagePackObject]
public sealed class DispatchStreamFrame
{
    /// <summary><see cref="DispatchFrameType"/> 常量之一。</summary>
    [Key(0)] public byte FrameType { get; set; }

    // ── Claim 帧 ────────────────────────────────────────────────────────────
    [Key(1)] public string? TaskId { get; set; }
    /// <summary>认领 worker 实例 id（计费 ChannelIdExternal 来源）。</summary>
    [Key(2)] public string? InstanceId { get; set; }
    [Key(3)] public string? Subject { get; set; }
    /// <summary>网关瀑布解析出的 conversationId。</summary>
    [Key(4)] public string? ConversationId { get; set; }
    /// <summary>conversationId 来源层（header / bodyKey / chain 等）。</summary>
    [Key(5)] public string? ConversationSource { get; set; }

    // ── Chunk 帧 ────────────────────────────────────────────────────────────
    /// <summary>MessagePack 序列化的 <c>StreamingChunkDto</c> 原始字节。</summary>
    [Key(6)] public byte[]? ChunkPayload { get; set; }

    // ── Error 帧 ────────────────────────────────────────────────────────────
    /// <summary>建议映射的 HTTP 状态码（503 无节点 / 502 流中断 / 400 上下文超限等）。</summary>
    [Key(7)] public int StatusCode { get; set; }
    [Key(8)] public string? ErrorType { get; set; }
    /// <summary>网关错误码（本地化查表键）。</summary>
    [Key(9)] public string? ErrorCode { get; set; }
    [Key(10)] public string? ErrorMessage { get; set; }
    [Key(11)] public Dictionary<string, string>? ErrorParams { get; set; }
}
