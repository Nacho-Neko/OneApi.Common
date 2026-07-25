using OneApi.Common.Vendors;

namespace OneApi.Common.Llm;

/// <summary>
/// 经网关派发一次 LLM 调用的最小契约：<b>路由 + 协议 + 账户</b>。
/// 任何新任务接入模型时都应组此 record，而不是只传 model 字符串或只传 vendor。
/// <list type="bullet">
///   <item><see cref="Target"/> — 渠道 plug + 模型名（<see cref="ModelRef"/>）；</item>
///   <item><see cref="CallerFormat"/> — 调用方协议（<see cref="WireFormat"/>）；</item>
///   <item><see cref="AccountUid"/> — 受信内网账户 id（替代 sk- 鉴权的 trust 值）。</item>
/// </list>
/// </summary>
public sealed record ModelInvoke(ModelRef Target, WireFormat CallerFormat, long AccountUid = 0)
{
    /// <summary>OpenAI Chat Completions 路径的便捷构造。</summary>
    public static ModelInvoke OpenAiChat(ModelRef target, long accountUid = 0) =>
        new(target, WireFormat.OpenAiChat, accountUid);

    /// <summary>路由是否完整（plug 与 model 均非空）。</summary>
    public bool IsRoutable =>
        !string.IsNullOrWhiteSpace(Target.VendorPlug)
        && !string.IsNullOrWhiteSpace(Target.Model);
}
