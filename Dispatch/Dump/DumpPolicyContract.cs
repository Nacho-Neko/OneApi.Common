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
/// 一条截获规则。规则之间怎么组合看 <see cref="DumpPolicyDoc.MatchMode"/>；<c>Enabled=false</c> 的规则不参与匹配。
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
/// 截获策略文档在 NATS 上的位置：OneApi 的库（<see cref="OneApiKv.Bucket"/>，一个 JetStream KV 桶）里 <see cref="Table"/> 这张表，
/// 整份文档就一个键。控制台（Meeko.Observability）带修订号写，网关 watch 这个键。
///
/// <para>不放 Consul（2026-11-27 起，之前在 <c>oneapi/config/dump</c>）：策略是 OneApi 自己的数据，网关本来就连 NATS；
/// Consul 只管服务发现和注册。键写死、不开放配置：控制台和网关配得不一样，网关就一直收不到策略，而且看不出为什么。</para>
/// </summary>
public static class DumpKv
{
    /// <summary>表名：截获策略在库里的键都是 <c>dump.</c> 开头。</summary>
    public const string Table = "dump";

    /// <summary>整份截获策略。</summary>
    public const string Key = Table + ".policy";
}

/// <summary>
/// 截获策略文档。控制台写进 NATS 上 OneApi 的库（<see cref="DumpKv.Key"/>），网关 watch 同一个键。
/// </summary>
public sealed record DumpPolicyDoc
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }

    /// <summary>
    /// 窗口终点。网关**本地**判定是否到期，所以关掉窗口不需要再写一次 KV，
    /// 一份过期的文档自己就会停止武装。
    /// </summary>
    [JsonPropertyName("until")] public DateTimeOffset? Until { get; init; }

    /// <summary>
    /// 规则之间怎么组合：<c>any</c>（或，缺省）/ <c>all</c>（且），取值见 <see cref="DumpMatchModes"/>。
    /// 网关不认识的值不会退回 <c>any</c>：那会把抓的范围放大，时间窗规则一条都不生效。
    /// </summary>
    [JsonPropertyName("matchMode")] public string MatchMode { get; init; } = DumpMatchModes.Any;

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
    /// 策略在我们自己这边的失败里匹配，外加一个要原样点名才抓的 <c>vendor_bad_request</c>（Vendor 回了 400
    /// 而我们认不出）；其余 Vendor 报的错、号池状态、调用方断开，怎么写都匹配不上，要看就开时间窗按错误码抓。
    /// 判定在 <see cref="DumpErrorCodes.MatchesOnError"/>。</para>
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
        GatewayErrorCode.VendorError,
        GatewayErrorCode.VendorBadRequest,
        GatewayErrorCode.VendorRefused,
        GatewayErrorCode.VendorPolicyRejected,
        GatewayErrorCode.ContextLengthExceeded,
        GatewayErrorCode.CircuitBroken,
        GatewayErrorCode.ParseError,
        GatewayErrorCode.WorkerError,
        GatewayErrorCode.VendorTimeout,
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
    /// <summary>报错抓包能抓、但必须在策略里原样点名的 Vendor 错误码，见 <see cref="MatchesOnError"/>。</summary>
    public static IReadOnlyList<string> NamedOnly { get; } =
        [GatewayErrorCode.VendorBadRequest, GatewayErrorCode.VendorPolicyRejected];

    public static bool IsOwnFailure(string? code)
        => code is not null
           && (code.StartsWith(TranscoderPrefix, StringComparison.Ordinal)
               || code is GatewayErrorCode.InternalError
                   or GatewayErrorCode.NoResponseBody
                   or GatewayErrorCode.WorkerError
                   or GatewayErrorCode.InvalidRequestBody);

    /// <summary>
    /// 报错抓包的策略匹配不匹配这次失败。网关（匹配）和控制台（预览）用同一份。
    ///
    /// <para>我们自己这边的失败（<see cref="IsOwnFailure"/>）按模式匹配，<c>*</c> 也算。</para>
    ///
    /// <para>Vendor 报的错里只有 <see cref="NamedOnly"/> 这两个能抓，而且必须在策略里<b>原样点名</b>，
    /// <c>*</c> 和 <c>vendor_*</c> 都带不上：量不由我们控制，要人明确写出来才开，不能被一条通配顺手带上。
    /// 照样可以用 <c>!</c> 排除。</para>
    /// <list type="bullet">
    ///   <item><see cref="GatewayErrorCode.VendorBadRequest"/>：「Vendor 回了 400 而我们认不出」的那一堆，多半是我们的请求体有问题
    ///   （某个 Vendor 下线一个模型就是一天上千条）。</item>
    ///   <item><see cref="GatewayErrorCode.VendorPolicyRejected"/>：Vendor 的内容围栏拦了（目前只有 Factory 的
    ///   <c>content_policy_rejected</c>）。围栏按固定句子拦，看请求原文能删出是哪一句，再写进 OneApi 网关的过滤规则，
    ///   以后网关自己先拒、客户不吃罚金（2026-11-28 起）。</item>
    /// </list>
    ///
    /// <para>其余 Vendor 报的错（拒答、限流、超窗、5xx）、号池状态怎么写都不匹配：那是 Vendor 对这次请求、
    /// 这个账号的判断，看请求原文也修不了什么。要看就开时间窗按错误码抓。</para>
    /// </summary>
    public static bool MatchesOnError(IReadOnlyList<string> patterns, string? code)
    {
        if (code is null || patterns.Count == 0) return false;

        if (IsOwnFailure(code))
            return DumpErrorPatterns.Matches(patterns, code);

        if (!NamedOnly.Contains(code, StringComparer.Ordinal)) return false;

        var named = false;
        foreach (var pattern in patterns)
        {
            if (pattern.Length == 0) continue;
            if (pattern[0] == DumpErrorPatterns.Exclude)
            {
                if (DumpErrorPatterns.Matches(pattern.AsSpan(1), code)) return false;
            }
            else if (pattern == code)
            {
                named = true;
            }
        }
        return named;
    }
}
