namespace OneApi.Common.Dispatch;

/// <summary>
/// Structured error codes used across gateway entry layers and OneApi workers.
/// Each code uniquely identifies an error scenario and drives localized messages at the edge.
/// </summary>
public static class GatewayErrorCode
{
    // ── Client request errors (gateway controller layer) ─────────────────────
    public const string InvalidRequestBody = "invalid_request_body";
    public const string EmptyMessages = "empty_messages";
    public const string EmptyInput = "empty_input";
    public const string AuthenticationFailed = "authentication_failed";
    public const string ModelRequired = "model_required";
    public const string FieldRequired = "field_required";
    public const string InvalidJson = "invalid_json";
    public const string AuthBackendUnavailable = "auth_backend_unavailable";

    // ── Gateway infrastructure errors (dispatcher / NATS) ────────────────────
    public const string NoNatsConnection = "no_nats_connection";
    public const string NoProviders = "no_providers";
    public const string ProvidersUnresponsive = "providers_unresponsive";
    public const string WorkerRejected = "worker_rejected";
    public const string GatewayTimeout = "gateway_timeout";
    public const string ProviderTimeout = "provider_timeout";
    public const string EmptyClaimReply = "empty_claim_reply";
    public const string NoResponseBody = "no_response_body";
    public const string InternalError = "internal_error";

    // 转码器的错误码不在这里：由 ChatTransit 自己定义（TransitErrorCode，都以 "ChatTransit." 开头）。

    // ── 过滤器（OneApi 网关按每个 Vendor 的过滤规则拒绝请求，见 OneApi 仓库 request-filter.md）──────────

    /// <summary>
    /// 这个 Vendor 的过滤器拒了这次请求。完整的码是前缀加拒掉它的那条过滤规则的 id
    /// （<c>filter_rejected.no-agents</c>）：拒绝原因就在码里，计费侧按码决定罚不罚，不另开字段。
    /// 回给终端客户端时去掉规则 id 只留 <c>filter_rejected</c>——告诉它是哪条规则拒的，等于教它怎么绕开。
    /// </summary>
    public const string FilterRejectedPrefix = "filter_rejected.";

    /// <summary>没有规则成立、按默认动作拒掉时，规则 id 的位置上写这个。过滤规则不能叫这个名字。</summary>
    public const string FilterDefaultRule = "default";

    public static string FilterRejected(string? ruleId)
        => FilterRejectedPrefix + (ruleId ?? FilterDefaultRule);

    public static bool IsFilterRejected(string? code)
        => code is not null && code.StartsWith(FilterRejectedPrefix, StringComparison.Ordinal);

    // ── Provider business errors ─────────────────────────────────────────────
    public const string CircuitBroken = "circuit_broken";
    public const string ModelNotFound = "model_not_found";
    public const string RateLimited = "rate_limited";
    public const string Unauthorized = "unauthorized";
    /// <summary>
    /// Vendor 回了 400，而我们认不出是哪一种：多半是我们发过去的请求体它不认，要拿请求原文来查。
    /// 认得出的（拒答、超窗）各有自己的码，不要落到这里——报错抓包按这个码抓「没见过的」。
    /// </summary>
    public const string VendorBadRequest = "vendor_bad_request";

    /// <summary>
    /// Vendor 收下了请求但不肯答（内容策略、判成不是求助）。跟请求体的写法无关，重发也一样，
    /// 所以从 <see cref="VendorBadRequest"/> 里分出来，不进报错抓包。不罚款。
    /// </summary>
    public const string VendorRefused = "vendor_refused";

    /// <summary>
    /// 拒绝：明确知道是 Vendor 平台自己的内容围栏拦了这次请求，模型没出过一个字。计费侧认这个码做惩罚性扣费。
    /// <para>
    /// 黑名单原则（2026-11-18 定的）：只有逐条登记过、明确知道是平台围栏触发的那个错误才报它，其余一律放过——
    /// 不按状态码、不按文案里的字眼、不按「看起来像内容问题」去猜；拿不准的留在 <see cref="VendorRefused"/> 或
    /// <see cref="VendorError"/>，宁可漏罚不可错罚。目前登记的只有一条：Factory 的 <c>content_policy_rejected</c>。
    /// 别的渠道（包括 Agentspace 的 SKIPPED）要加，先拿到线上实例确认是围栏，再登记。
    /// </para>
    /// 和拒答（模型自己不答：200、有输出、照常按用量扣）不是一回事。
    /// </summary>
    public const string VendorPolicyRejected = "vendor_policy_rejected";

    public const string ContextLengthExceeded = "context_length_exceeded";

    /// <summary>Vendor 那边出了错（5xx、过载、403、回了空内容）：不是请求体的问题，换个账号或稍后再试可能就好。</summary>
    public const string VendorError = "vendor_error";

    /// <summary>等 Vendor 的回包等过了时限。</summary>
    public const string VendorTimeout = "vendor_timeout";

    // ── 2026-11-18 之前的 wire 值：对端一律叫 Vendor，「upstream」这个叫法不再用 ─────────────
    // 没升级的 Provider 还在发这三个旧值，接收方用 Normalize 归一；用量日志里的历史行也还是旧值。
    // 所有 Provider 升级完，Legacy* 和 Normalize 里对应的分支就可以删。
    public const string LegacyUpstreamBadRequest = "upstream_bad_request";
    public const string LegacyUpstreamError = "upstream_error";
    public const string LegacyUpstreamTimeout = "upstream_timeout";

    [Obsolete("改名为 VendorBadRequest（wire 值也改成了 vendor_bad_request）。")]
    public const string UpstreamBadRequest = VendorBadRequest;

    [Obsolete("改名为 VendorError（wire 值也改成了 vendor_error）。")]
    public const string UpstreamError = VendorError;

    [Obsolete("改名为 VendorTimeout（wire 值也改成了 vendor_timeout）。")]
    public const string UpstreamTimeout = VendorTimeout;

    public const string ConnectionError = "connection_error";
    public const string ParseError = "parse_error";
    public const string WorkerError = "worker_error";

    // ── Streaming-specific errors ────────────────────────────────────────────
    public const string StreamInterrupted = "stream_interrupted";
    public const string StreamFailed = "stream_failed";

    // ── Billing / reserve failure codes ──────────────────────────────────────
    public const string BillingUnavailable = "billing_unavailable";
    public const string BillingHoldFailed = "billing_hold_failed";
    public const string InsufficientBalance = "insufficient_balance";
    public const string WalletFrozen = "wallet_frozen";
    public const string SubAccountLimitExceeded = "sub_account_limit_exceeded";
    public const string RiskBlocked = "risk_blocked";
    public const string AmountExceeded = "amount_exceeded";
    public const string BillingSystemError = "system_error";
    public const string RatioMissing = "ratio_missing";
    public const string TokenNotFound = "token_not_found";
    public const string TokenDisabled = "token_disabled";
    public const string TokenExpired = "token_expired";
    public const string TokenExhausted = "token_exhausted";
    public const string TokenAccountMismatch = "token_account_mismatch";
    public const string TokenLimitExceeded = "token_limit_exceeded";
    public const string ModelNotAllowed = "model_not_allowed";
    public const string ReservationExpired = "reservation_expired";

    /// <summary>
    /// 把改名前的 wire 值换成现在的。滚动发布时新网关会收到旧 Provider 的码，不归一的话
    /// 状态码映射、抓包规则、遥测都会把同一种错误当成两种。在收 Provider 回包的入口调一次。
    /// </summary>
    public static string? Normalize(string? code) => code switch
    {
        LegacyUpstreamBadRequest => VendorBadRequest,
        LegacyUpstreamError => VendorError,
        LegacyUpstreamTimeout => VendorTimeout,
        _ => code,
    };
}
