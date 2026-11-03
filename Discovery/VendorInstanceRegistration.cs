using Consul;
using Microsoft.Extensions.Logging;
using Platform.Common.Discovery;

namespace OneApi.Common.Discovery;

/// <summary>
/// 一个厂商实例（provider 副本）的注册：Name = 厂商 wire 值（<c>claude</c>、<c>agentspace</c>…），ID = 实例 ID，
/// 标签 <see cref="InstanceTagSchema.WorkerIdentifyingTag"/>；TTL 检查，每 10 秒续期；Meta 只带身份和生命周期状态。
/// 业务树上是 <c>oneapi/vendor/{厂商}/{实例}</c>：账号绑定在实例上，实例是它厂商分组下的成员。
/// </summary>
/// <remarks>
/// 注册的是实例本身——每个副本一条，永远挂在规范的厂商名下；NATS 派发组是路由层面的事，不出现在注册里。
/// 忙不忙不自报，网关从 NATS 推断。VaultCustody 从这里读 worker 是否在线。
/// <para>实例此刻接不接活（<see cref="IsServing"/>）由宿主给出：它变了，下一轮就重新注册成 healthy / draining。</para>
/// </remarks>
public abstract class VendorInstanceRegistration(
    IConsulClient consul,
    string vendorKind,
    string vendorName,
    string instanceId,
    ILogger logger) : ConsulRegistration(consul, logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    protected override TimeSpan Interval => TimeSpan.FromSeconds(10);

    /// <summary>本实例此刻是否在接活。为 false 时注册成 draining，网关不再往这里派发。</summary>
    protected abstract bool IsServing { get; }

    protected override AgentServiceRegistration Describe()
    {
        var meta = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [InstanceTagSchema.MetaWorkerInstanceId] = instanceId,
            [InstanceTagSchema.MetaWorkerKind] = vendorKind,
            [InstanceTagSchema.MetaStatus] = IsServing ? "healthy" : "draining",
        };
        // 实例 ID 来自 yaml，可能带大写或别的字符：路径段规范化，原值放显示名。厂商不导出遥测，没有 service.name。
        WorldServiceMeta.Write(meta, new WorldDeclaration(
            null,
            $"oneapi/vendor/{vendorKind}/{ToSegment(instanceId)}",
            $"OneApi/Vendor/{vendorName}/{instanceId.Replace('/', '-')}",
            null,
            null));

        return new AgentServiceRegistration
        {
            ID = instanceId,
            Name = vendorKind,
            Tags = [InstanceTagSchema.WorkerIdentifyingTag],
            Meta = meta,
            Check = new AgentServiceCheck
            {
                TTL = Ttl,
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                Notes = $"Provider worker TTL for {instanceId}",
            },
        };
    }

    private static string ToSegment(string raw)
    {
        var segment = new string(raw.Trim().ToLowerInvariant()
            .Select(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') ? c : '-')
            .ToArray()).Trim('-');
        return segment.Length == 0 ? "unknown" : segment;
    }
}
