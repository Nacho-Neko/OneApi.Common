namespace OneApi.Common.Llm.Clients;

/// <summary>
/// 各 provider 的 LlmClient 继承它。由 <see cref="LlmClientPool"/> 经账号的公共对象
/// （<see cref="LlmClientContext"/>）创建：账号级共享的连接、缓存、熔断都在公共对象上，构造一个槽只挂它的引用。
///
/// <para>状态只由槽自己推进：<see cref="TryReserve"/> 空闲→占住，<see cref="RunAsync"/> 占住→执行→冷却→空闲
/// （在 finally 里走完，执行怎么结束都会回到空闲），<see cref="Release"/> 占住→空闲。</para>
///
/// <para>HttpClient 不用自己管：公共对象按创建时指定的 <see cref="HttpClientStrategy"/> 给，
/// 槽被回收时交还给它，该不该释放由策略决定。</para>
/// </summary>
public abstract class LlmClient : ILlmClient
{
    private readonly object _httpGate = new();
    private int _state = (int)LlmClientState.Idle;
    private long _idleSince;
    private HttpClient? _http;
    private LlmClientPool? _pool;
    private CancellationTokenSource? _cancel;
    private Task _work = Task.CompletedTask;
    private int _disposed;

    protected LlmClient(LlmClientContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    /// <summary>账号的公共对象。</summary>
    protected LlmClientContext Context { get; }

    public LlmClientState State => (LlmClientState)Volatile.Read(ref _state);

    public bool IsProcessing => State is LlmClientState.Reserved or LlmClientState.Processing;

    /// <summary>
    /// 这个槽用的 HttpClient，第一次用时向公共对象要。共用、私有还是静态，由公共对象创建时指定的
    /// <see cref="HttpClientStrategy"/> 决定，生命周期也归它管。
    /// </summary>
    protected HttpClient Http
    {
        get
        {
            var http = Volatile.Read(ref _http);
            if (http is not null)
                return http;
            lock (_httpGate)
                return _http ??= Context.HttpFor();
        }
    }

    /// <summary>空闲到现在的时间戳（<see cref="LlmClientPool.Time"/> 的刻度），回收按它判断闲了多久。</summary>
    internal long IdleSince => Volatile.Read(ref _idleSince);

    /// <summary>手上的任务；没在执行时是已完成的任务。</summary>
    internal Task Work => Volatile.Read(ref _work);

    public bool TryReserve() => Move(LlmClientState.Idle, LlmClientState.Reserved);

    public void Release()
    {
        if (Move(LlmClientState.Reserved, LlmClientState.Idle))
            BecameIdle();
    }

    public async Task RunAsync(Func<CancellationToken, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!Move(LlmClientState.Reserved, LlmClientState.Processing))
            throw new InvalidOperationException($"{GetType().Name} must be reserved before it runs (state {State}).");

        using var cancel = new CancellationTokenSource();
        Volatile.Write(ref _cancel, cancel);
        try
        {
            var task = work(cancel.Token);
            Volatile.Write(ref _work, task);
            await task;
        }
        finally
        {
            Volatile.Write(ref _cancel, null);
            await CoolDownAsync();
        }
    }

    public void Cancel()
    {
        try
        {
            Volatile.Read(ref _cancel)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 刚好执行完
        }
    }

    /// <summary>被池子回收或池子释放时调。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>把 HttpClient 交还公共对象（每槽私有的随之释放）。子类要额外清理就覆盖，记得调 base。</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Volatile.Read(ref _http) is { } http)
            Context.Return(http);
    }

    internal void Attach(LlmClientPool pool)
    {
        _pool = pool;
        Volatile.Write(ref _idleSince, pool.Time.GetTimestamp());
    }

    /// <summary>空闲才回收得掉；回收后再也占不到。</summary>
    internal bool TryRetire() => Move(LlmClientState.Idle, LlmClientState.Retired);

    private async Task CoolDownAsync()
    {
        var pool = _pool;
        if (pool is { Cooldown: var cooldown } && cooldown > TimeSpan.Zero)
        {
            Volatile.Write(ref _state, (int)LlmClientState.CoolingDown);
            await Task.Delay(cooldown, pool.Time);
        }

        Volatile.Write(ref _state, (int)LlmClientState.Idle);
        BecameIdle();
    }

    private void BecameIdle()
    {
        var pool = _pool;
        if (pool is null)
            return;
        Volatile.Write(ref _idleSince, pool.Time.GetTimestamp());
        pool.OnReturned();
    }

    private bool Move(LlmClientState from, LlmClientState to)
        => Interlocked.CompareExchange(ref _state, (int)to, (int)from) == (int)from;
}
