using MagicOnion;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// Edge → Gateway 高保真 LLM 派发服务（MagicOnion server streaming / unary）。
///
/// <para>与面向 PG 路径的 <c>IInternalLlmInvokeService</c>（有损、结构化 chunk）不同，本契约为
/// 公开 API 边缘服务（DemuxAi.Edge）设计：流式帧透传网关内部的
/// <c>StreamingChunkDto</c>（MessagePack 序列化字节，见 <see cref="DispatchStreamFrame.ChunkPayload"/>），
/// Edge 侧反序列化后可字节级重建三家原生 SSE 协议（含 RawSse 直通、reasoning 签名等），
/// 不丢失任何上游信息。</para>
///
/// <para>职责边界：调用方（Edge）负责鉴权、计费 Reserve/Commit、模型别名 → vendor 解析与
/// payload 的 model 改写；Gateway 只负责派发——多池派发组路由、conversationId 瀑布
/// （header 提示 → prompt-cache key → 会话链）、prompt-cache 亲和与分钟级调用统计。</para>
/// </summary>
public interface ILlmDispatchService : IService<ILlmDispatchService>
{
    /// <summary>非流式派发：一次性返回完整上游响应（结构化 chunk 包或原生 JSON 直通）。</summary>
    UnaryResult<DispatchReply> DispatchAsync(DispatchCommand command);

    /// <summary>
    /// 流式派发。帧序：认领成功后先回一帧 <see cref="DispatchFrameType.Claim"/>
    /// （taskId / instanceId / 解析出的 conversationId，供调用方做计费归因），
    /// 随后零或多帧 <see cref="DispatchFrameType.Chunk"/>，流自然结束即成功；
    /// 任何派发/上游错误以一帧 <see cref="DispatchFrameType.Error"/> 结束。
    /// </summary>
    Task<ServerStreamingResult<DispatchStreamFrame>> DispatchStreamingAsync(DispatchCommand command);
}
