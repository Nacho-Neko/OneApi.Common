using Consul;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Balancer;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OneApi.Common.Dispatch;

/// <summary>
/// 连 OneApi 网关：<c>gwconsul:///Gateway</c> 发现健康实例，RoundRobin，h2c。
///
/// <para>心跳是 HTTP/2 PING（<see cref="KeepAlivePingPolicy.Always"/>），间隔 10 秒、超时 30 秒。
/// 超时设得比一次 GC 停顿长，避免把还活着的连接拆掉——拆掉时这条连接上所有在途流一起变成
/// Cancelled。不设 <see cref="SocketsHttpHandler.PooledConnectionLifetime"/>，寿命到期同样会
/// GOAWAY。半死连接靠 ping 发现；实例上下线靠解析器的 Refresh。</para>
///
/// <para>重连退避封在 10 秒。grpc-dotnet 默认封顶 120 秒，subchannel 一旦进退避，下游恢复了
/// 也不会重连，直到进程重启。退避只在传入 <see cref="GrpcChannelOptions.HttpHandler"/>
/// （而不是 HttpClient）时生效。</para>
/// </summary>
public static class OneApiGatewayChannel
{
    /// <summary>与 OneApi <c>GatewaySelfRegistrar.ServiceName</c> 一致。</summary>
    public const string ServiceName = "Gateway";

    public const string Scheme = "gwconsul";

    internal static readonly TimeSpan KeepAlivePingDelay = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan KeepAlivePingTimeout = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan InitialReconnectBackoff = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan MaxReconnectBackoff = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 打开到 <paramref name="serviceName"/> 的通道。默认是 OneApi 网关。
    /// 调用方持有并在进程结束时 <see cref="GrpcChannel.ShutdownAsync"/>。
    /// </summary>
    public static GrpcChannel Connect(
        IConsulClient consul,
        ILoggerFactory loggerFactory,
        string serviceName = ServiceName)
    {
        ArgumentNullException.ThrowIfNull(consul);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name is required.", nameof(serviceName));

        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSingleton<ResolverFactory>(new OneApiGatewayResolverFactory(consul));

        var options = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = GrpcMessageLimits.MaxBytes,
            MaxSendMessageSize = GrpcMessageLimits.MaxBytes,
            Credentials = ChannelCredentials.Insecure,
            ServiceProvider = services.BuildServiceProvider(),
            ServiceConfig = RoundRobin(),
        };
        ApplyTransport(options);
        return GrpcChannel.ForAddress($"{Scheme}:///{serviceName}", options);
    }

    /// <summary>固定地址，不走 Consul。心跳和重连与 <see cref="Connect(IConsulClient, ILoggerFactory, string)"/> 相同。</summary>
    public static GrpcChannel Connect(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));

        var options = new GrpcChannelOptions { ServiceConfig = UnavailableRetryOnly() };
        ApplyTransport(options);
        return GrpcChannel.ForAddress(address, options);
    }

    internal static void ApplyTransport(GrpcChannelOptions options)
    {
        options.HttpHandler = CreateHandler();
        options.InitialReconnectBackoff = InitialReconnectBackoff;
        options.MaxReconnectBackoff = MaxReconnectBackoff;
    }

    internal static SocketsHttpHandler CreateHandler() => new()
    {
        EnableMultipleHttp2Connections = true,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        // 只在没有在途请求时才有机会回收。在途流不会被这里掐掉。
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = KeepAlivePingDelay,
        KeepAlivePingTimeout = KeepAlivePingTimeout,
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
    };

    private static ServiceConfig RoundRobin() => new()
    {
        LoadBalancingConfigs = { new RoundRobinConfig() },
        MethodConfigs = { UnavailableRetry() },
    };

    private static ServiceConfig UnavailableRetryOnly() => new()
    {
        MethodConfigs = { UnavailableRetry() },
    };

    private static MethodConfig UnavailableRetry() => new()
    {
        Names = { MethodName.Default },
        RetryPolicy = new RetryPolicy
        {
            MaxAttempts = 3,
            InitialBackoff = TimeSpan.FromMilliseconds(200),
            MaxBackoff = TimeSpan.FromSeconds(3),
            BackoffMultiplier = 2,
            RetryableStatusCodes = { StatusCode.Unavailable },
        },
    };
}
