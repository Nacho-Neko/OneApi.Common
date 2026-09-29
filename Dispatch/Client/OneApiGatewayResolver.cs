using Consul;
using Grpc.Core;
using Grpc.Net.Client.Balancer;
using Microsoft.Extensions.Logging;
using GrpcStatus = Grpc.Core.Status;

namespace OneApi.Common.Dispatch;

/// <summary>
/// <c>gwconsul:///&lt;service&gt;</c>：解析 Consul 里该服务全部健康实例的 MagicOnion 端点。
/// OneApi 网关以服务名 <see cref="OneApiGatewayChannel.ServiceName"/> 注册，
/// meta <c>meeko-grpc-url</c> 携带 h2c 地址。
///
/// <para>首次 RPC 时做一次非阻塞查询；之后用 blocking query 听实例增减，index 不变不发布。
/// 后台线程只调用 <see cref="PollingResolver.Refresh"/>，不直接 <c>Listener</c>，
/// 避开 grpc-dotnet 的 Picker 中毒（issue #2407）。</para>
/// </summary>
internal sealed class OneApiGatewayResolver(Uri address, IConsulClient consul, ILoggerFactory loggerFactory)
    : PollingResolver(loggerFactory)
{
    private const string GrpcUrlMetaKey = "meeko-grpc-url";

    private static readonly TimeSpan WaitTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly string _serviceName = ParseServiceName(address);
    private readonly ILogger _logger = loggerFactory.CreateLogger<OneApiGatewayResolver>();
    private readonly CancellationTokenSource _watchCts = new();
    private int _watchStarted;
    private int _seeded;
    private ulong _lastIndex;
    private volatile ServiceEntry[]? _snapshot;
    private List<BalancerAddress> _lastGoodAddresses = [];

    protected override async Task ResolveAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _seeded, 1) == 0)
        {
            try
            {
                var result = await consul.Health.Service(
                    _serviceName, string.Empty, passingOnly: true, cancellationToken);
                _lastIndex = result.LastIndex;
                _snapshot = result.Response;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Initial OneApi gateway resolve for '{Service}' failed", _serviceName);
            }
            finally
            {
                StartWatchLoop();
            }
        }

        PublishSnapshot();
    }

    private void StartWatchLoop()
    {
        if (Interlocked.Exchange(ref _watchStarted, 1) == 0)
            _ = Task.Run(() => WatchLoopAsync(_watchCts.Token));
    }

    private async Task WatchLoopAsync(CancellationToken cancellationToken)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var queryOptions = new QueryOptions
                {
                    WaitIndex = _lastIndex,
                    WaitTime = WaitTime,
                };

                var result = await consul.Health.Service(
                    _serviceName, string.Empty, passingOnly: true, queryOptions, cancellationToken);

                // index < 1 不可信。下一轮若 WaitIndex=0，blocking query 会退化成忙轮询。
                var newIndex = result.LastIndex < 1 ? 1 : result.LastIndex;

                if (newIndex < _lastIndex)
                {
                    _lastIndex = 0;
                }
                else if (newIndex != _lastIndex)
                {
                    _lastIndex = newIndex;
                    _snapshot = result.Response;
                    Refresh();
                }

                backoff = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex, "OneApi gateway watch for '{Service}' failed; retrying in {Backoff}s",
                    _serviceName, backoff.TotalSeconds);
                try
                {
                    await Task.Delay(backoff, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (backoff < MaxBackoff)
                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
            }
        }
    }

    // 查不到健康实例时保留上次的地址，不发 ForFailure 拆掉在途流。
    // 真正的实例增减由 watch → Refresh 驱动。
    private void PublishSnapshot()
    {
        var entries = _snapshot ?? [];
        var addresses = new List<BalancerAddress>(entries.Length);
        foreach (var entry in entries)
        {
            if (TryResolveGrpcAddress(entry, out var uri))
                addresses.Add(new BalancerAddress(uri.Host, uri.Port));
        }

        if (addresses.Count > 0)
        {
            _lastGoodAddresses = addresses;
            _logger.LogDebug(
                "Resolved {Count} OneApi gateway endpoint(s) for '{Service}'",
                addresses.Count, _serviceName);
            Listener(ResolverResult.ForResult(addresses));
            return;
        }

        if (_lastGoodAddresses.Count > 0)
        {
            _logger.LogWarning(
                "No healthy OneApi gateway instances for '{Service}'; keeping {Count} last-known-good endpoint(s)",
                _serviceName, _lastGoodAddresses.Count);
            Listener(ResolverResult.ForResult(_lastGoodAddresses));
            return;
        }

        Listener(ResolverResult.ForFailure(new GrpcStatus(
            StatusCode.Unavailable,
            $"No healthy OneApi gateway instances found in Consul for '{_serviceName}'.")));
    }

    private static bool TryResolveGrpcAddress(ServiceEntry entry, out Uri uri)
    {
        var meta = entry.Service.Meta;
        if (meta is not null
            && meta.TryGetValue(GrpcUrlMetaKey, out var raw)
            && Uri.TryCreate(raw, UriKind.Absolute, out var fromMeta))
        {
            uri = fromMeta;
            return true;
        }

        var host = string.IsNullOrWhiteSpace(entry.Service.Address) ? entry.Node.Address : entry.Service.Address;
        if (!string.IsNullOrWhiteSpace(host) && entry.Service.Port > 0)
        {
            uri = new Uri($"http://{host}:{entry.Service.Port}");
            return true;
        }

        uri = null!;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watchCts.Cancel();
            _watchCts.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string ParseServiceName(Uri address)
        => !string.IsNullOrEmpty(address.Host) ? address.Host : address.AbsolutePath.Trim('/');
}

internal sealed class OneApiGatewayResolverFactory(IConsulClient consul) : ResolverFactory
{
    public override string Name => OneApiGatewayChannel.Scheme;

    public override Resolver Create(ResolverOptions options)
        => new OneApiGatewayResolver(options.Address, consul, options.LoggerFactory);
}
