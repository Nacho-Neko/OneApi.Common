namespace OneApi.Common.Providers.Streaming;

/// <summary>Gemini <c>candidatesTokensDetails</c> 里按模态累加的输出 token。</summary>
public readonly record struct CandidateModalityTokens(long Image, long Audio, long Video);
