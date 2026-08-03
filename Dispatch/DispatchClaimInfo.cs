namespace OneApi.Common.Dispatch;

/// <summary>
/// Per-claim metadata from a successful dispatch claim frame.
/// Used by HTTP entry layers to enrich billing logs with worker instance,
/// executing account, and task id.
/// </summary>
public record DispatchClaimInfo(
    string TaskId,
    string InstanceId,
    string Subject,
    string? AccountKey = null);
