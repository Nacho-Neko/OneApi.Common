using Consul;

namespace OneApi.Common.Discovery;

/// <summary>
/// String constants for the tag / meta layout that process-level worker
/// registrations (<see cref="VendorInstanceRegistration"/>) pack into Consul, and that
/// VaultCustody / Gateway readers unpack from the cluster catalog.
/// </summary>
public static class InstanceTagSchema
{
    /// <summary>
    /// Tag on every Provider worker registration. Readers use this to find
    /// all workers when <c>Service Name</c> is the channel kind (e.g. <c>claude</c>).
    /// </summary>
    public const string WorkerIdentifyingTag = "provider-worker";

    /// <summary>Meta key: raw process instance id (matches placement / KV docs).</summary>
    public const string MetaWorkerInstanceId = "workerInstanceId";
    /// <summary>Meta key: channel kind (e.g. <c>kiro</c>).</summary>
    public const string MetaWorkerKind = "workerKind";
    /// <summary>Meta key for the canonical provider id (legacy fallback for worker kind).</summary>
    public const string MetaProviderId = "providerId";
    /// <summary>Meta key for the lifecycle status (<c>healthy</c> / <c>draining</c>).</summary>
    public const string MetaStatus = "status";

    /// <summary>Whether <paramref name="tags"/> include the worker identifying tag.</summary>
    public static bool HasWorkerTag(IEnumerable<string>? tags)
    {
        if (tags is null) return false;
        foreach (var tag in tags)
        {
            if (string.Equals(tag, WorkerIdentifyingTag, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>Whether <paramref name="svc"/> is a Provider worker registration.</summary>
    public static bool IsProviderWorker(AgentService svc)
        => HasWorkerTag(svc.Tags);
}
