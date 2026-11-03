using Consul;
using Microsoft.Extensions.Logging;
using OneApi.Common.Dispatch;
using Platform.Common.Discovery;

namespace OneApi.Common.Discovery;

/// <summary>本网关副本向 Consul 发布的身份：主机（本机连 Consul 的源 IP）、gRPC 端口、遥测 service.name。</summary>
public sealed record OneApiGatewayInstance(string Host, int GrpcPort, string TelemetryServiceName);

/// <summary>
/// OneApi.Gateway 的注册。服务名固定 <see cref="OneApiGatewayChannel.ServiceName"/>，调用方（Yomi / Tavern 网关）经
/// <c>gwconsul:///Gateway</c> 按名字找到全部健康副本轮询；gRPC 健康检查；业务树上是 <c>oneapi/gateway</c>。
/// </summary>
/// <remarks>
/// ID 每副本唯一、同一副本重启不变（<c>{服务名}/{主机}:{端口}</c>）：重启的实例直接覆盖自己崩溃前留下的那条，
/// 不会多出一条等 1 分钟才过期的僵尸；ID 若可配，两个副本配成一样就会互相顶掉。
/// </remarks>
public sealed class OneApiGatewayRegistration(
    IConsulClient consul,
    OneApiGatewayInstance instance,
    ILogger<OneApiGatewayRegistration> logger) : ConsulRegistration(consul, logger)
{
    private readonly AgentServiceRegistration _registration = Build(instance);

    protected override AgentServiceRegistration Describe() => _registration;

    private static AgentServiceRegistration Build(OneApiGatewayInstance instance)
    {
        var endpoint = $"{instance.Host}:{instance.GrpcPort}";
        var meta = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ConsulServiceMeta.GrpcUrl] = $"http://{endpoint}",
            [ConsulServiceMeta.BaseUrl] = $"http://{endpoint}/",
        };
        WorldServiceMeta.Write(meta, new WorldDeclaration(
            instance.TelemetryServiceName, "oneapi/gateway", "OneApi/Gateway", null, null));

        return new AgentServiceRegistration
        {
            ID = $"{OneApiGatewayChannel.ServiceName}/{endpoint}",
            Name = OneApiGatewayChannel.ServiceName,
            Address = instance.Host,
            Port = instance.GrpcPort,
            Tags = ["meeko"],
            Meta = meta,
            // grpc.health.v1（h2c 明文）：探的是 gRPC 管道和 NATS 健康，进程卡死、NATS 断连都会翻 critical。
            Check = new AgentServiceCheck
            {
                GRPC = endpoint,
                GRPCUseTLS = false,
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(3),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
            },
        };
    }
}
