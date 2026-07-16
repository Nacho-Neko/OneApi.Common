namespace OneApi.Common.Llm;

/// <summary>
/// OpenAI 兼容对话消息 role 常量。任何构造 LLM 请求（补全 / agent / 唤醒）时都应使用此处的值，
/// 不要手写 <c>"user"</c> / <c>"assistant"</c> 字符串。
/// </summary>
public static class ChatRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";

    public static bool IsKnown(string? role) =>
        role is System or User or Assistant or Tool;
}
