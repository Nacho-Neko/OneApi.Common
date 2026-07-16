namespace OneApi.Common.Llm;

/// <summary>
/// 流式补全结束原因常量（OpenAI 兼容 <c>finish_reason</c>）。
/// </summary>
public static class FinishReasons
{
    public const string Stop = "stop";
    public const string ToolCalls = "tool_calls";
    public const string Length = "length";
    public const string ContentFilter = "content_filter";

    /// <summary>
    /// 面向终端用户的 finish_reason：内部 agent 机制（如 tool_calls）不暴露给客户端，
    /// 统一呈现为 <see cref="Stop"/>。
    /// </summary>
    public static string ForClientDisplay(string? reason) =>
        reason == ToolCalls ? Stop : reason ?? Stop;
}
