using MessagePack;
using OneApi.Common.Llm;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// LLM 派发命令。调用方（受信内网服务，如 DemuxAi.Edge）已完成鉴权、计费预扣与
/// 模型别名解析：别名 → <see cref="VendorKey"/> + <see cref="VendorModel"/>。
/// </summary>
[MessagePackObject]
public sealed class DispatchCommand
{
    /// <summary>路由 vendor_key（NATS 基础队列组；多池渠道由网关按路由表二次改写）。</summary>
    [Key(0)] public string VendorKey { get; set; } = string.Empty;

    /// <summary>
    /// 渠道真实模型名（路由表的 <c>vendor_model</c>）：多池派发路由与统计的依据，
    /// 并由网关带外转交 provider。协议若把 model 放在 body 里，<see cref="PayloadJson"/>
    /// 中的值与此一致；Gemini 形态的 payload 则没有该字段，本字段是唯一来源。
    /// </summary>
    [Key(1)] public string VendorModel { get; set; } = string.Empty;

    /// <summary>原生协议请求体（UTF-8 JSON）。</summary>
    [Key(2)] public byte[] PayloadJson { get; set; } = [];

    /// <summary>
    /// 调用方协议格式。决定原生直通（NativeFormat 匹配时透传）与 conversationId
    /// 提取器的解析方式。与 <c>TaskEnvelope.CallerFormat</c> 同为枚举：本契约两端
    /// 都是自家服务，没有理由在这一跳退化成自由字符串再解析回来。
    /// <see cref="WireFormat.Unspecified"/> 表示调用方未声明。
    /// </summary>
    [Key(3)] public WireFormat CallerFormat { get; set; }

    /// <summary>
    /// conversationId 第一层提示（调用方从 <c>x-conversation-id</c> 请求头取得）。
    /// 为空时网关继续走 prompt-cache key 提取 → 会话链推导的瀑布。
    /// </summary>
    [Key(4)] public string? ConversationIdHint { get; set; }

    /// <summary>调用方请求关联 id（trace id），仅用于日志串联。</summary>
    [Key(5)] public string? RequestId { get; set; }
}
