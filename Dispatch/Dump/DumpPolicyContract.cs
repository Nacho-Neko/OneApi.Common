using System.Text.Json.Serialization;

namespace OneApi.Common.Dispatch.Dump;

/// <summary>
/// 截获规则的种类。**这是控制台与网关之间的 wire 契约**，
/// 序列化名由 <see cref="DumpRuleKinds.ToWire"/> 给出。
/// </summary>
public enum DumpRuleKind
{
    /// <summary>调用方。今天匹配 sk- token id（DispatchCommand.AffinityScope）。</summary>
    Caller,

    /// <summary>vendor_key。注意不是 NATS 派发组，填派发组永不命中。</summary>
    VendorKey,

    /// <summary>上游裸模型名。</summary>
    VendorModel,

    /// <summary>事后规则：按 GatewayErrorCode 匹配，可逗号分隔多选。</summary>
    ErrorCode,

    /// <summary>事后规则：对响应原文做子串匹配，不解析协议。</summary>
    ErrorMessage,
}

/// <summary>
/// 枚举与 wire 字符串的唯一映射。
///
/// <para><b>为什么放共享库。</b> 生产端（控制台）与消费端（网关）对未知值的**处理策略**
/// 不同——生产端严格拒绝、消费端宽松降级——但**取值集合本身是同一个**。
/// 之前两边各写一份枚举，等于把同一个契约打了两份字，改一边不会有任何编译期信号。
/// 策略差异属于各自的 JsonConverter，不属于枚举。</para>
/// </summary>
public static class DumpRuleKinds
{
    /// <summary>规范 wire 名。写出去一律用这个。</summary>
    public static string ToWire(DumpRuleKind kind) => kind switch
    {
        DumpRuleKind.Caller => "caller",
        DumpRuleKind.VendorKey => "vendorKey",
        DumpRuleKind.VendorModel => "vendorModel",
        DumpRuleKind.ErrorCode => "error_code",
        DumpRuleKind.ErrorMessage => "error_message",
        _ => kind.ToString(),
    };

    /// <summary>
    /// 解析 wire 名。别名用于兼容 vendor → vendorKey / model → vendorModel 那次改名，
    /// 让两代控制台写的策略都能编译通过。
    /// </summary>
    public static bool TryFromWire(string? raw, out DumpRuleKind kind)
    {
        switch (raw)
        {
            case "caller": kind = DumpRuleKind.Caller; return true;
            case "vendorKey" or "vendor": kind = DumpRuleKind.VendorKey; return true;
            case "vendorModel" or "model": kind = DumpRuleKind.VendorModel; return true;
            case "error_code" or "errorCode": kind = DumpRuleKind.ErrorCode; return true;
            case "error_message" or "errorMessage": kind = DumpRuleKind.ErrorMessage; return true;
            default: kind = default; return false;
        }
    }

    /// <summary>全部规范 wire 名，控制台用来渲染下拉框。</summary>
    public static IReadOnlyList<string> AllWireNames { get; } =
        Enum.GetValues<DumpRuleKind>().Select(ToWire).ToArray();
}

/// <summary>
/// 一条截获规则。规则之间为「或」；<c>Enabled=false</c> 的规则不参与匹配。
/// </summary>
public sealed record DumpRuleDoc
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";

    /// <summary>
    /// <c>null</c> 表示读取方不认识这个 kind。
    ///
    /// <para>本类型**不挂 <c>[JsonConverter]</c>**：读取策略由各端在
    /// <c>JsonSerializerOptions</c> 上自己挂——网关宽松（未知→null，只丢这一条规则），
    /// 控制台严格（未知→拒绝写入）。挂在属性上会把其中一种策略焊死到契约里。</para>
    /// </summary>
    [JsonPropertyName("kind")] public DumpRuleKind? Kind { get; init; }

    [JsonPropertyName("value")] public string Value { get; init; } = "";

    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
}

/// <summary>
/// 截获策略文档。控制台写入 Consul KV <c>oneapi/config/dump</c>，
/// 网关用 blocking query 监听同一个键。
/// </summary>
public sealed record DumpPolicyDoc
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }

    /// <summary>
    /// 窗口终点。网关**本地**判定是否到期，所以关掉窗口不需要再写一次 KV，
    /// 一份过期的文档自己就会停止武装。
    /// </summary>
    [JsonPropertyName("until")] public DateTimeOffset? Until { get; init; }

    /// <summary>目前只有 <c>any</c>（规则之间 OR）；留在 wire 上是为了向前兼容。</summary>
    [JsonPropertyName("matchMode")] public string MatchMode { get; init; } = "any";

    [JsonPropertyName("rules")] public DumpRuleDoc[] Rules { get; init; } = [];

    [JsonPropertyName("updatedAt")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// 截获点可达的错误码。
///
/// <para><see cref="GatewayErrorCode"/> 的常量里只有这三组能在截获点出现：
/// 计费/鉴权那组发生在 Demux edge（请求根本没进过 LlmDispatchService），
/// 参数校验那组属于早已不存在的 controller 层。把不可达的塞进控制台下拉框是陷阱。</para>
/// </summary>
public static class DumpErrorCodes
{
    /// <summary>「任意失败」哨兵，不是 GatewayErrorCode 的成员。</summary>
    public const string AnyErrorSentinel = "any_error";

    public static IReadOnlyList<string> Reachable { get; } =
    [
        AnyErrorSentinel,
        // 网关基础设施：dispatcher 自身产生
        GatewayErrorCode.NoProviders,
        GatewayErrorCode.ProvidersUnresponsive,
        GatewayErrorCode.GatewayTimeout,
        GatewayErrorCode.NoResponseBody,
        GatewayErrorCode.InternalError,
        // Provider 业务：来自 TaskReplyError.Code
        GatewayErrorCode.RateLimited,
        GatewayErrorCode.UpstreamError,
        GatewayErrorCode.UpstreamBadRequest,
        GatewayErrorCode.ContextLengthExceeded,
        GatewayErrorCode.CircuitBroken,
        GatewayErrorCode.ParseError,
        GatewayErrorCode.WorkerError,
        GatewayErrorCode.UpstreamTimeout,
        // 流式专属
        GatewayErrorCode.StreamInterrupted,
        GatewayErrorCode.StreamFailed,
    ];

    public static bool IsReachable(string code) => Reachable.Contains(code, StringComparer.Ordinal);
}
