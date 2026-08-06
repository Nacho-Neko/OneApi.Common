using MessagePack;

namespace OneApi.Common.Dispatch;

/// <summary>
/// A single streaming response chunk, MessagePack-serialized for dispatch transport
/// (e.g. opaque chunk payloads on Demux dispatch stream frames).
/// </summary>
[MessagePackObject]
public partial class StreamingChunkDto
{
    [Key(0)] public string? AuthorRole { get; set; }
    [Key(1)] public StreamingContentType ContentType { get; set; }
    [Key(2)] public string? Text { get; set; }
    [Key(3)] public string? FunctionName { get; set; }
    [Key(4)] public string? FunctionCallId { get; set; }
    [Key(5)] public string? FunctionArguments { get; set; }
    [Key(6)] public bool Done { get; set; }
    [Key(7)] public string? Error { get; set; }
    [Key(8)] public Dictionary<string, long>? Usage { get; set; }
    [Key(9)] public string? ModelId { get; set; }
    [Key(10)] public string? FinishReason { get; set; }
    [Key(11)] public string? ErrorCode { get; set; }
    [Key(12)] public Dictionary<string, string>? ErrorParams { get; set; }

    /// <summary>
    /// Opaque cryptographic signature the upstream provider attached to this block
    /// (Anthropic <c>thinking.signature</c>, Gemini <c>thoughtSignature</c>). Set on
    /// <see cref="StreamingContentType.Thinking"/> chunks and — for Gemini 3, which
    /// signs the <c>functionCall</c> part and validates it on replay — on
    /// <see cref="StreamingContentType.FunctionCall"/> chunks as well.
    /// </summary>
    [Key(13)] public string? ReasoningSignature { get; set; }
}

public enum StreamingContentType : byte
{
    Text = 0,
    FunctionCall = 1,
    Usage = 2,
    Thinking = 3,
    RawSse = 4,
}
