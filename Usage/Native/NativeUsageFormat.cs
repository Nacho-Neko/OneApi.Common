namespace OneApi.Common.Usage.Native;

/// <summary>
/// 上游原生响应的协议。读原文里的 usage 按它挑读取器（<see cref="NativeUsageReaders"/>）；
/// 网关数请求体和已吐出文本时也按它挑字段和分词器。没有读取器的格式（生图）读不出 LLM 用量。
/// </summary>
public enum NativeUsageFormat
{
    OpenAiChat,
    OpenAiResponses,
    OpenAiImages,
    Anthropic,
    Gemini,
    NovelAi,
}
