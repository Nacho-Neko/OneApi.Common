using MessagePack;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>非流式派发结果；镜像网关内部 <c>ProviderResponse</c> 并附带认领/会话归因信息。</summary>
[MessagePackObject]
public sealed class DispatchReply
{
    [Key(0)] public bool IsSuccess { get; set; }

    /// <summary>上游/网关状态码（成功恒为 200；503 无节点、504 超时等）。</summary>
    [Key(1)] public int StatusCode { get; set; }

    /// <summary>
    /// 结构化响应：MessagePack 序列化的 <c>List&lt;StreamingChunkDto&gt;</c>
    /// （Gateway.Shared 类型；调用方持有同一程序集自行反序列化）。
    /// 与 <see cref="RawResponseJson"/> 互斥。
    /// </summary>
    [Key(2)] public byte[]? BodyBytes { get; set; }

    /// <summary>原生直通响应（上游 NativeFormat 与 CallerFormat 匹配时的完整 JSON 字节）。</summary>
    [Key(3)] public byte[]? RawResponseJson { get; set; }

    /// <summary>上游回报的原生格式标识（如 <c>anthropic</c>）。</summary>
    [Key(4)] public string? NativeFormat { get; set; }

    [Key(5)] public DispatchErrorInfo? Error { get; set; }

    // ── 认领 / 归因（成功认领后填充） ───────────────────────────────────────
    [Key(6)] public string? TaskId { get; set; }
    /// <summary>认领本次任务的 Provider 进程 id（yaml Instance.Id）。</summary>
    [Key(7)] public string? InstanceId { get; set; }
    [Key(8)] public string? Subject { get; set; }

    /// <summary>网关瀑布解析出的 conversationId（header 提示 → cache key → 会话链）。</summary>
    [Key(9)] public string? ConversationId { get; set; }
    /// <summary>conversationId 的来源层（header / bodyKey / chain 等），仅用于日志。</summary>
    [Key(10)] public string? ConversationSource { get; set; }

    /// <summary>实际执行账号（<c>AccountId.ToString("N")</c>），供亲和写回与归因。</summary>
    [Key(11)] public string? AccountKey { get; set; }
}

/// <summary>派发错误详情；Code/Params 供调用方走本地化文案。</summary>
[MessagePackObject]
public sealed class DispatchErrorInfo
{
    /// <summary>服务端原始错误消息（英文，日志用；用户可见文案请按 Code 本地化）。</summary>
    [Key(0)] public string? Message { get; set; }

    /// <summary>协议错误类型（如 <c>no_providers</c> / <c>provider_stream_error</c>）。</summary>
    [Key(1)] public string? Type { get; set; }

    /// <summary>网关错误码（<c>GatewayErrorCode</c> 常量），本地化查表键。</summary>
    [Key(2)] public string? Code { get; set; }

    /// <summary>本地化文案的占位参数。</summary>
    [Key(3)] public Dictionary<string, string>? Params { get; set; }
}
