namespace OneApi.Common.Llm.Clients;

/// <summary><see cref="LlmClientPool"/> 的容量与回收设置。</summary>
public sealed record LlmClientPoolOptions
{
    /// <summary>
    /// 最小数量：建池时先建好这么多，空闲回收也只回收到这里。各渠道一般只是上限不同，最小数量都一样；
    /// 超过上限时按上限算。
    /// </summary>
    public int MinCapacity { get; init; } = 2;

    /// <summary>上限：最多同时有几个槽。<c>null</c> = 不设上限（忙时一直加）；<c>0</c> = 不用这个池。</summary>
    public int? MaxCapacity { get; init; }

    /// <summary>每个槽执行完后的冷却，冷却完才能再占。</summary>
    public TimeSpan Cooldown { get; init; }

    /// <summary>
    /// 空闲多久回收（只回收多于最小数量的部分）。<see cref="Timeout.InfiniteTimeSpan"/> = 不自动回收，
    /// 只在调 <see cref="LlmClientPool.TrimIdle"/> 时回收。
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>计时（冷却、空闲时长、回收定时）。测试可以换。</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;

    /// <summary>
    /// 后台回收、释放槽时出的错交给这里（定时回收跑在线程池上，异常不能往外抛）。用的地方应当接上日志。
    /// </summary>
    public Action<Exception>? OnError { get; init; }
}

/// <summary>
/// 一组 <see cref="LlmClient"/>，像缓冲池一样用：<see cref="TryReserve"/> 占一个空闲的，
/// <see cref="ILlmClient.RunAsync"/> 执行完它自己回到空闲。
///
/// <para>槽由池子经 <see cref="LlmClientContext"/> 创建：建池时先建最小数量个，都占着且没到上限就再建，
/// 空闲超过 <see cref="LlmClientPoolOptions.IdleTimeout"/> 的回收到最小数量，省内存。
/// 数组只在扩、缩时整份替换，扫描读不可变快照；占用、执行、归还改的是那个槽自己的状态，不改池子。</para>
///
/// <para>「几个在执行」「还能占几个」都是现扫每个槽的状态算出来的，没有计数器。</para>
/// </summary>
public sealed class LlmClientPool : IDisposable
{
    // 注销排空时，占住但还没交执行的槽没有任务可等，隔这么久再扫一次。
    private static readonly TimeSpan DrainRescan = TimeSpan.FromMilliseconds(50);

    private readonly LlmClientContext _context;
    private readonly Action<Exception>? _onError;
    private readonly object _resize = new();
    private readonly ITimer? _trimTimer;
    private readonly TimeSpan _idleTimeout;
    private LlmClient[] _clients;
    private bool _disposed;

    public LlmClientPool(LlmClientContext context, LlmClientPoolOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxCapacity must not be negative.");
        if (options.MinCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MinCapacity must not be negative.");

        _context = context;
        _onError = options.OnError;
        _idleTimeout = options.IdleTimeout;
        Time = options.Time;
        Cooldown = options.Cooldown;
        MaxCapacity = options.MaxCapacity;
        MinCapacity = Math.Min(options.MinCapacity, options.MaxCapacity ?? int.MaxValue);

        _clients = new LlmClient[MinCapacity];
        for (var i = 0; i < _clients.Length; i++)
            _clients[i] = Create();

        // 上限等于最小数量的池子永远不会多出来，用不着回收。
        if (_idleTimeout != Timeout.InfiniteTimeSpan && MaxCapacity != MinCapacity)
            _trimTimer = Time.CreateTimer(static state => ((LlmClientPool)state!).TrimInBackground(),
                this, _idleTimeout, _idleTimeout);
    }

    /// <summary>上限；<c>null</c> = 不设上限。</summary>
    public int? MaxCapacity { get; }

    /// <summary>最小数量（已按上限截过）。</summary>
    public int MinCapacity { get; }

    public TimeSpan Cooldown { get; }

    public TimeProvider Time { get; }

    /// <summary>有槽回到空闲（执行完、冷却完，或者占了没派就还了）。</summary>
    public event Action<LlmClientPool>? ClientReturned;

    /// <summary>当前建好的槽（不可变快照）。</summary>
    public IReadOnlyList<ILlmClient> Clients => Volatile.Read(ref _clients);

    /// <summary>正在执行（含占住还没开始）的槽数，现扫出来的。</summary>
    public int Processing
    {
        get
        {
            var n = 0;
            foreach (var client in Volatile.Read(ref _clients))
            {
                if (client.IsProcessing)
                    n++;
            }
            return n;
        }
    }

    /// <summary>还能占几个：上限减去不空闲的（执行中或冷却中）；不设上限时是 <see cref="int.MaxValue"/>。</summary>
    public int Available
    {
        get
        {
            if (MaxCapacity is not { } max)
                return int.MaxValue;

            var busy = 0;
            foreach (var client in Volatile.Read(ref _clients))
            {
                if (client.State != LlmClientState.Idle)
                    busy++;
            }
            return Math.Max(0, max - busy);
        }
    }

    public bool HasRoom => Available > 0;

    /// <summary>
    /// 占一个空闲的槽；都不空闲且没到上限就新建一个。占不到返回 null。
    /// 占到的槽要么 <see cref="ILlmClient.RunAsync"/>，要么 <see cref="ILlmClient.Release"/>。
    /// </summary>
    public ILlmClient? TryReserve()
    {
        foreach (var client in Volatile.Read(ref _clients))
        {
            if (client.TryReserve())
                return client;
        }

        lock (_resize)
        {
            if (_disposed || _clients.Length >= (MaxCapacity ?? int.MaxValue))
                return null;

            // 先占到手再放进池子，别人拿不走。
            var created = Create();
            if (!created.TryReserve())
                throw new InvalidOperationException("A fresh LlmClient must be reservable.");
            Volatile.Write(ref _clients, [.. _clients, created]);
            return created;
        }
    }

    /// <summary>
    /// 回收空闲超过 <see cref="LlmClientPoolOptions.IdleTimeout"/> 的槽，回收到最小数量为止，回收掉的立即释放。
    /// 返回回收了几个。平时由池子按回收时长自动做。
    /// </summary>
    public int TrimIdle()
    {
        var retired = new List<LlmClient>();
        lock (_resize)
        {
            if (_disposed)
                return 0;

            var clients = _clients;
            var kept = new List<LlmClient>(clients.Length);
            foreach (var client in clients)
            {
                // 先让它再也占不到，再从池子里拿掉：拿着旧快照的派发方也占不到一个正在回收的。
                if (clients.Length - retired.Count > MinCapacity
                    && client.State == LlmClientState.Idle
                    && Time.GetElapsedTime(client.IdleSince) >= _idleTimeout
                    && client.TryRetire())
                {
                    retired.Add(client);
                    continue;
                }
                kept.Add(client);
            }

            if (retired.Count > 0)
                Volatile.Write(ref _clients, kept.ToArray());
        }

        DisposeAll(retired);
        return retired.Count;
    }

    /// <summary>等所有槽都不在执行（冷却不等）。注销账号时用，之后再 <see cref="Dispose"/>。</summary>
    public async Task WhenIdleAsync()
    {
        while (true)
        {
            var busy = false;
            var pending = new List<Task>();
            foreach (var client in Volatile.Read(ref _clients))
            {
                if (!client.IsProcessing)
                    continue;
                busy = true;
                var work = client.Work;
                if (!work.IsCompleted)
                    pending.Add(work);
            }

            if (!busy)
                return;

            // 占住还没交执行的槽手上没有任务可等，隔一会儿再扫。
            pending.Add(Task.Delay(DrainRescan, Time));
            await Task.WhenAny(pending);
        }
    }

    /// <summary>停掉回收、释放所有槽。先 <see cref="WhenIdleAsync"/>，别把正在用的释放了。</summary>
    public void Dispose()
    {
        LlmClient[] clients;
        lock (_resize)
        {
            if (_disposed)
                return;
            _disposed = true;
            clients = _clients;
        }

        _trimTimer?.Dispose();
        DisposeAll(clients);
    }

    internal void OnReturned() => ClientReturned?.Invoke(this);

    private LlmClient Create()
    {
        var client = _context.CreateClient()
                     ?? throw new InvalidOperationException($"{_context.GetType().Name}.CreateClient returned null.");
        client.Attach(this);
        return client;
    }

    private void TrimInBackground()
    {
        try
        {
            TrimIdle();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }

    // 一个释放失败不耽误其余的。
    private void DisposeAll(IEnumerable<LlmClient> clients)
    {
        foreach (var client in clients)
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
        }
    }
}
