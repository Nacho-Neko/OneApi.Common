using MessagePack;

namespace OneApi.Common.Dispatch;

/// <summary>
/// Provider 到网关这一跳的内部帧。公开入口不读它：网关编成调用方协议的 SSE
/// （<see cref="Meeko.Contracts.Demux.Dispatch.DispatchFrameType.Sse"/>）再交回去。
/// 渠道若还留着上游原文，只填 <see cref="StreamingContentType.RawSse"/> 和 <see cref="Text"/>。
/// </summary>
[MessagePackObject]
public partial class StreamingChunkDto
{
    [Key(0)] public StreamingContentType ContentType { get; set; }

    [Key(1)] public string? AuthorRole { get; set; }

    [Key(2)] public string? ModelId { get; set; }

    [Key(3)] public bool Done { get; set; }

    /// <summary>正文、思考，或一行原始 SSE。哪种由 <see cref="ContentType"/> 决定。</summary>
    [Key(4)] public string? Text { get; set; }

    /// <summary>上游给思考块或工具调用附带的签名。</summary>
    [Key(5)] public string? ReasoningSignature { get; set; }

    /// <summary>
    /// 终止原因。Gemini 的思考块会把这里标成 <c>thought</c>，那是标签，不是终止。
    /// </summary>
    [Key(6)] public string? FinishReason { get; set; }

    [Key(7)] public ToolCallChunk? ToolCall { get; set; }

    [Key(8)] public Dictionary<string, long>? Usage { get; set; }

    [Key(9)] public ChunkError? Error { get; set; }

    public static StreamingChunkDto Failed(string message, string? code, Dictionary<string, string>? parms = null)
        => new()
        {
            Done = true,
            Error = new ChunkError { Message = message, Code = code, Params = parms },
        };
}

/// <summary>一次工具调用。名字和 id 在开始时给出，参数 JSON 随后按片段追加。</summary>
[MessagePackObject]
public sealed class ToolCallChunk
{
    [Key(0)] public string? Name { get; set; }

    [Key(1)] public string? Id { get; set; }

    [Key(2)] public string? Arguments { get; set; }
}

/// <summary>这一帧失败时的错误。成功帧不带。</summary>
[MessagePackObject]
public sealed class ChunkError
{
    [Key(0)] public string? Message { get; set; }

    [Key(1)] public string? Code { get; set; }

    [Key(2)] public Dictionary<string, string>? Params { get; set; }
}

public enum StreamingContentType : byte
{
    Text = 0,
    FunctionCall = 1,
    Usage = 2,
    Thinking = 3,
    RawSse = 4,
}
