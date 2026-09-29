using Consul;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Client;
using Microsoft.Extensions.Logging;

namespace OneApi.Common.Dispatch;

/// <summary>
/// 一条 gwconsul 上的 MagicOnion 客户端。通道、心跳和重连都在这里。
/// 要调哪个服务，把契约放进 <typeparamref name="TService"/>。
/// </summary>
public sealed class OneApiGatewayClient<TService> : IAsyncDisposable
    where TService : IService<TService>
{
    private readonly GrpcChannel _channel;

    public OneApiGatewayClient(
        IConsulClient consul,
        ILoggerFactory loggerFactory,
        string serviceName = OneApiGatewayChannel.ServiceName,
        params IClientFilter[] filters)
    {
        _channel = OneApiGatewayChannel.Connect(consul, loggerFactory, serviceName);
        Service = MagicOnionClient.Create<TService>(_channel, filters);
    }

    public TService Service { get; }

    public async ValueTask DisposeAsync() => await _channel.ShutdownAsync().ConfigureAwait(false);
}
