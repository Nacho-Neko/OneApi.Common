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

    /// <summary>Vendor 的裸模型名。</summary>
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

    /// <summary>
    /// 报错抓包的策略：一组错误码模式，网关拿每次失败的错误码去匹配，匹配上就抓这次的请求。例如
    /// <c>["ChatTransit.*", "!ChatTransit.Request.Malformed", "worker_error"]</c>。
    /// 写法见 <see cref="DumpErrorPatterns"/>。不走 <see cref="Enabled"/>/<see cref="Until"/> 窗口——
    /// 请求字节本来就活到调用结束，不必武装流式缓冲。
    ///
    /// <para>下发的是策略，不是错误清单：错误码分层命名（<c>ChatTransit.Request.NotConvertible</c>），
    /// 一条 <c>ChatTransit.*</c> 就管住整个转码器，以后新加的错误不用改配置。</para>
    ///
    /// <para>没写或空数组就是不抓：抓包会把请求原文写进遥测，只在人明确打开时才抓。
    /// 策略只在我们自己这边的失败里匹配；Vendor 报的错、号池状态、调用方断开，怎么写都匹配不上，
    /// 要看 Vendor 报错就开时间窗按错误码抓。</para>
    /// </summary>
    [JsonPropertyName("onError")] public string[]? OnError { get; init; }

    [JsonPropertyName("updatedAt")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// 错误码模式的写法和匹配，网关（匹配）和控制台（校验）用同一份。
/// <list type="bullet">
///   <item><c>*</c> 匹配任意一段字符：<c>ChatTransit.*</c>、<c>ChatTransit.Request.*</c>、<c>*</c>。</item>
///   <item>不带 <c>*</c> 就是整个错误码完全相同：<c>worker_error</c>。区分大小写。</item>
///   <item>以 <c>!</c> 开头是排除：前面的模式匹配上了、又被排除的，不算匹配。和书写顺序无关。</item>
/// </list>
/// </summary>
public static class DumpErrorPatterns
{
    /// <summary>排除模式的前缀。</summary>
    public const char Exclude = '!';

    /// <summary>模式里允许的字符：错误码用到的字母、数字、<c>_</c>、<c>.</c>、<c>-</c>，加上 <c>*</c>。</summary>
    public static bool IsValid(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        var body = pattern[0] == Exclude ? pattern.AsSpan(1) : pattern.AsSpan();
        if (body.IsEmpty) return false;
        foreach (var c in body)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-' or '*'))
                return false;
        }
        return true;
    }

    /// <summary>一条模式（不带 <c>!</c>）是否匹配这个错误码。</summary>
    public static bool Matches(ReadOnlySpan<char> pattern, ReadOnlySpan<char> code)
    {
        var star = pattern.IndexOf('*');
        if (star < 0) return pattern.SequenceEqual(code);

        // 第一段必须在开头，最后一段必须在结尾，中间各段按顺序出现。
        var head = pattern[..star];
        if (!code.StartsWith(head, StringComparison.Ordinal)) return false;
        code = code[head.Length..];
        pattern = pattern[(star + 1)..];

        while ((star = pattern.IndexOf('*')) >= 0)
        {
            var part = pattern[..star];
            var at = code.IndexOf(part, StringComparison.Ordinal);
            if (at < 0) return false;
            code = code[(at + part.Length)..];
            pattern = pattern[(star + 1)..];
        }

        return code.EndsWith(pattern, StringComparison.Ordinal);
    }

    /// <summary>一组模式（含排除）是否匹配这个错误码：至少一条匹配，且没有被任何一条排除。</summary>
    public static bool Matches(IReadOnlyList<string> patterns, string code)
    {
        var included = false;
        foreach (var pattern in patterns)
        {
            if (pattern.Length == 0) continue;
            if (pattern[0] == Exclude)
            {
                if (Matches(pattern.AsSpan(1), code)) return false;
            }
            else if (!included && Matches(pattern, code))
            {
                included = true;
            }
        }
        return included;
    }
}

/// <summary>
/// 截获点可达的错误码。
///
/// <para><see cref="GatewayErrorCode"/> 的常量里只有这三组能在截获点出现：
/// 计费/鉴权那组发生在产品 edge（Yomi.Gateway 等；请求根本没进过 OneApi 的 LlmDispatchService），
/// 参数校验那组属于早已不存在的 controller 层。把不可达的塞进控制台下拉框是陷阱。</para>
/// </summary>
public static class DumpErrorCodes
{
    /// <summary>「任意失败」哨兵，不是 GatewayErrorCode 的成员。</summary>
    public const string AnyErrorSentinel = "any_error";

    /// <summary>
    /// 转码器错误码的前缀。具体的码由 ChatTransit 自己定义（<c>TransitErrorCode</c>：
    /// <c>ChatTransit.Request.NotConvertible</c> 等），这个库不引用 ChatTransit，只认前缀。
    /// </summary>
    public const string TranscoderPrefix = "ChatTransit.";

    /// <summary>整个转码器的错误，给时间窗的错误码规则用。</summary>
    public const string AnyTranscoderError = TranscoderPrefix + "*";

    public static IReadOnlyList<string> Reachable { get; } =
    [
        AnyErrorSentinel,
        // 网关基础设施：dispatcher 自身产生
        GatewayErrorCode.NoProviders,
        GatewayErrorCode.ProvidersUnresponsive,
        GatewayErrorCode.GatewayTimeout,
        GatewayErrorCode.NoResponseBody,
        GatewayErrorCode.InternalError,
        // 网关的转码器
        AnyTranscoderError,
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

    /// <summary>名单里的码，或者任何一个转码器的码（含带 <c>*</c> 的写法）。</summary>
    public static bool IsReachable(string code)
        => Reachable.Contains(code, StringComparer.Ordinal)
           || (code.StartsWith(TranscoderPrefix, StringComparison.Ordinal) && DumpErrorPatterns.IsValid(code));

    /// <summary>
    /// 这个错误码是不是我们自己这边的失败——报错抓包的策略只在这些里匹配：
    /// 转码器的（<see cref="TranscoderPrefix"/> 开头）、网关自己抛了异常或派发器出错（<c>internal_error</c>）、
    /// 派发器没收到回包正文（<c>no_response_body</c>）、Provider 处理时抛了异常（<c>worker_error</c>）、
    /// Provider 读不懂网关发过去的请求（<c>invalid_request_body</c>）。
    /// Vendor 报的错、号池状态都不是。
    /// </summary>
    public static bool IsOwnFailure(string? code)
        => code is not null
           && (code.StartsWith(TranscoderPrefix, StringComparison.Ordinal)
               || code is GatewayErrorCode.InternalError
                   or GatewayErrorCode.NoResponseBody
                   or GatewayErrorCode.WorkerError
                   or GatewayErrorCode.InvalidRequestBody);
}
