namespace OneApi.Common.Vendors;

/// <summary>
/// 调用一次模型所需的最小路由引用：<b>渠道 + 模型</b>，缺一不可。
/// 任何新任务（对话 / embedding / 生图 / 语音 / 视频 / agent 工具）接入模型调用时都应以此为准，
/// 不要只传一个"模型名"字符串。
/// <list type="bullet">
///   <item><see cref="VendorPlug"/>：渠道 slug（Demux <c>vendors.vendor_slug</c> / <c>queue_group</c>，
///     Tavern <c>chat_sessions.model_vendor_plug</c>）。null = 用网关默认渠道。</item>
///   <item><see cref="Model"/>：模型名。可以是对外别名（Demux <c>model_aliases.alias</c>）
///     或上游真实模型名（<c>vendor_model</c>）；网关派发时按渠道解析。</item>
/// </list>
/// </summary>
public sealed record ModelRef(string? VendorPlug, string Model)
{
    /// <summary>构造并按公共规则归一：plug 小写校验、model trim + 长度校验。</summary>
    public static ModelRef Create(string? vendorPlug, string model) => new(
        VendorRouting.TryNormalizeSlug(vendorPlug),
        VendorRouting.NormalizeVendorModel(model));
}
