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

    // ── Provider business errors ─────────────────────────────────────────────
    public const string CircuitBroken = "circuit_broken";
    public const string ModelNotFound = "model_not_found";
    public const string RateLimited = "rate_limited";
    public const string Unauthorized = "unauthorized";
    public const string UpstreamBadRequest = "upstream_bad_request";
    public const string ContextLengthExceeded = "context_length_exceeded";
    public const string UpstreamError = "upstream_error";
    public const string UpstreamTimeout = "upstream_timeout";
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
}
