using Microsoft.Extensions.Logging;

namespace OneApi.Common.Pricing.Relay;

/// <summary>一次去平台取价格表的结果。</summary>
/// <param name="Changed">表的内容和手上那份不一样了。</param>
/// <param name="Count">现在手上这份有多少行。</param>
/// <param name="SubscribersFailed">通知订阅的人去重算时有人抛了；下一次再取时会再通知一遍。</param>
public readonly record struct ModelPriceRefresh(bool Changed, int Count, bool SubscribersFailed);

/// <summary>
/// 产品这一头的价格表：向平台要（<see cref="IModelPriceRelayService"/>），进程里留一份现成的 <see cref="Current"/>，
/// 内容确实变了才通知订阅的人去重算有效价。
/// <para>
/// 什么时候去要：进程启动时要一次（<see cref="RunAsync"/>，平台还没起来就退避着重试，补上停机期间漏掉的），
/// 之后平台在价格表变了的时候调进来、Jobs 每小时兜底调一次——都是调宿主的定时任务入口，
/// 宿主在 <see cref="ModelPriceRelay.RefreshTaskKey"/> 这个键上转给 <see cref="RefreshAsync"/>。
/// 这里自己不起定时器、不挂着请求等。
/// </para>
/// <para>
/// 取不到不等于没有价格：平台够不着、它自己还没收到、回来的东西读不出来、某个厂家的文档写坏了，
/// 都留着手上最后一份好的。订阅的人看到的表只会从「一份好的」换成「另一份好的」。
/// </para>
/// <para>
/// 怎么调平台由宿主给（<see cref="Fetch"/>）：本库不引平台的服务发现和凭据那一套。
/// </para>
/// </summary>
public sealed class ModelPriceTableFeed
{
    /// <summary>调 <see cref="IModelPriceRelayService.GetAsync"/>。</summary>
    public delegate Task<byte[]> Fetch(string knownFingerprint, CancellationToken ct);

    private static readonly TimeSpan MinRetry = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetry = TimeSpan.FromSeconds(60);

    private readonly Fetch _fetch;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // 每个厂家最后一次解析成功的结果。回来的表里某个厂家的文档写坏了，靠它留住上一份。
    private Dictionary<string, List<PublishedModelPrice>> _lastGood = new(StringComparer.Ordinal);

    private volatile ModelPriceTable _current = ModelPriceTable.Empty;
    // 平台那一份的指纹，下次原样带回去。只有整份收下了才记：收到一半出错的话下次还得整份要。
    private string _relayFingerprint = string.Empty;
    private ulong _revision;
    // 上一次通知时有订阅的人抛了（重算没成）。表已经换成新的了，再去取平台会说「没变」，所以得自己记着下次补一遍。
    private bool _renotify;

    public ModelPriceTableFeed(Fetch fetch, ILogger<ModelPriceTableFeed> logger)
    {
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>手上最新的一份；还没取到过是 <see cref="ModelPriceTable.Empty"/>。</summary>
    public ModelPriceTable Current => _current;

    /// <summary>已经从平台取到过至少一次（取到的可以是空表）。</summary>
    public bool Loaded => _firstLoad.Task.IsCompleted;

    /// <summary>
    /// 表的内容变了，第一次取到非空的表也算。在 <see cref="RunAsync"/> 开始之前订阅，就不会漏掉第一份。
    /// 一个一个按订阅的先后调，谁抛了只记日志、不挡后面的；抛过的话下一次 <see cref="RefreshAsync"/> 会再通知一遍。
    /// </summary>
    public event Func<ModelPriceTable, CancellationToken, Task>? Changed;

    /// <summary>
    /// 等第一次取完。到点还没取到就回 false——平台够不着的时候宿主不该卡死在这里，照样往下起，之后取到了 <see cref="Changed"/> 会通知。
    /// </summary>
    public async Task<bool> WaitForFirstLoadAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        try
        {
            await _firstLoad.Task.WaitAsync(timeout, ct);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// 进程启动时跑：一直取到成功一次为止，然后返回。之后的更新不靠它，靠平台调进来（<see cref="RefreshAsync"/>）。
    /// </summary>
    public async Task RunAsync(CancellationToken stopping)
    {
        var delay = MinRetry;
        while (!stopping.IsCancellationRequested && !Loaded)
        {
            try
            {
                await RefreshAsync(stopping);
                return;
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Fetching the model price table from the platform failed; no table yet, retrying in {Delay}. Prices already compiled stay in use.",
                    delay);
            }

            try { await Task.Delay(delay, stopping); }
            catch (OperationCanceledException) { return; }
            delay = TimeSpan.FromSeconds(Math.Min(MaxRetry.TotalSeconds, delay.TotalSeconds * 2));
        }
    }

    /// <summary>
    /// 去平台取一次。取不到、读不出来就抛，手上那份不动——调用方（定时任务入口）把失败报回去，平台那头看得见。
    /// 同一时刻只跑一个：平台的通知和 Jobs 的兜底撞在一起时排着来。
    /// </summary>
    public async Task<ModelPriceRefresh> RefreshAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var packed = await _fetch(_relayFingerprint, ct) ?? [];
            if (packed.Length == 0)
            {
                // 平台那份没变。上一次有人没重算成的话，趁这一趟补上。
                _firstLoad.TrySetResult();
                if (!_renotify) return new ModelPriceRefresh(false, _current.Count, false);
                _renotify = !await NotifyAsync(_current, ct);
                return new ModelPriceRefresh(false, _current.Count, _renotify);
            }

            var envelope = ModelPriceRelayEnvelope.Unpack(packed);

            var makers = new Dictionary<string, List<PublishedModelPrice>>(StringComparer.Ordinal);
            foreach (var (key, value) in envelope.Documents)
            {
                // 表里不是厂家的键（记主人的那个）不当成价格
                if (ModelPriceKeys.MakerOf(key) is not { } maker) continue;
                if (TryParse(maker, value, key) is { } prices)
                    makers[maker] = prices;
                else if (_lastGood.TryGetValue(maker, out var previous))
                    // 这个厂家的文档写坏了：它上一份好的接着用，别的厂家照常换
                    makers[maker] = previous;
            }

            var table = new ModelPriceTable(
                makers.OrderBy(m => m.Key, StringComparer.Ordinal).SelectMany(m => m.Value),
                ++_revision);

            var first = !Loaded;
            var changed = !string.Equals(table.Fingerprint, _current.Fingerprint, StringComparison.Ordinal);
            // 这一份里没有的厂家就是删了：不留它的「上一份好的」，免得哪天它的键写坏了又把删掉的价带回来
            _lastGood = makers;
            _current = table;
            _relayFingerprint = envelope.Fingerprint;
            _firstLoad.TrySetResult();

            if (first)
                _logger.LogInformation("Model price table received from the platform: {Count} prices.", table.Count);
            // 第一次取到的非空表也算「变了」。内容一样（平台那头的指纹变了、解析出来的价没变）不通知：订阅的人不必白白重算。
            if (!changed && !_renotify)
                return new ModelPriceRefresh(false, table.Count, false);
            if (changed && !first)
                _logger.LogInformation("Model price table changed: {Count} prices.", table.Count);

            _renotify = !await NotifyAsync(table, ct);
            return new ModelPriceRefresh(changed, table.Count, _renotify);
        }
        finally
        {
            _gate.Release();
        }
    }

    private List<PublishedModelPrice>? TryParse(string maker, byte[]? value, string key)
    {
        if (value is not { Length: > 0 }) return [];
        try
        {
            var doc = PublishedMakerPrices.FromBytes(value) ?? throw new FormatException("document is null");
            foreach (var price in doc.Prices)
                price.Maker = maker;
            return doc.Prices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model price document {Key} is malformed; keeping this maker's previous prices.", key);
            return null;
        }
    }

    /// <summary>都通知到了回 true；有人抛了回 false（已经记了日志）。</summary>
    private async Task<bool> NotifyAsync(ModelPriceTable table, CancellationToken ct)
    {
        if (Changed is not { } handlers) return true;
        var allOk = true;
        foreach (var handler in handlers.GetInvocationList().Cast<Func<ModelPriceTable, CancellationToken, Task>>())
        {
            try
            {
                await handler(table, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                allOk = false;
                _logger.LogError(ex, "A model price table subscriber failed; it will be notified again on the next refresh.");
            }
        }
        return allOk;
    }
}
