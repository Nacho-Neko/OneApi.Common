using MessagePack;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// LLM 派发命令。调用方（受信内网服务，如 DemuxAi.Edge）已完成鉴权、计费预扣与
/// 模型别名解析；payload 的 <c>model</c> 字段已被改写为真实上游模型名。
/// </summary>
[MessagePackObject]
public sealed class DispatchCommand
{
    /// <summary>路由 vendor_key（NATS 基础队列组；多池渠道由网关按路由表二次改写）。</summary>
    [Key(0)] public string VendorKey { get; set; } = string.Empty;

    /// <summary>真实上游模型名（payload 中 model 字段的当前值）；用于多池派发路由与统计。</summary>
    [Key(1)] public string UpstreamModel { get; set; } = string.Empty;

    /// <summary>原生协议请求体（UTF-8 JSON，model 已改写）。</summary>
    [Key(2)] public byte[] PayloadJson { get; set; } = [];

    /// <summary>
    /// 调用方协议格式：<c>openai.chat</c> | <c>openai.responses</c> | <c>anthropic</c> | <c>gemini</c>。
    /// 决定原生直通（NativeFormat 匹配时透传）与 conversationId 提取器的解析方式。
    /// </summary>
    [Key(3)] public string CallerFormat { get; set; } = string.Empty;

    /// <summary>
    /// conversationId 第一层提示（调用方从 <c>x-conversation-id</c> 请求头取得）。
    /// 为空时网关继续走 prompt-cache key 提取 → 会话链推导的瀑布。
    /// </summary>
    [Key(4)] public string? ConversationIdHint { get; set; }

    /// <summary>调用方请求关联 id（trace id），仅用于日志串联。</summary>
    [Key(5)] public string? RequestId { get; set; }
}
