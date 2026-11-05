namespace OneApi.Common.Llm.Clients;

/// <summary>
/// 账号（或账号下一个组）的公共对象：所有槽共用的东西都放在它上面——连接、缓存、熔断、账号快照——
/// 它也是槽的工厂。<see cref="LlmClientPool"/> 通过 <see cref="CreateClient"/> 建槽，建一个只是挂引用，所以扩容很快。
///
/// <para>槽的 HttpClient 由这里按 <see cref="HttpStrategy"/> 给出、收回并管生命周期（创建时指定），槽只管用。</para>
///
/// <para>由账号持有、账号结束时释放；一个公共对象可以给多个池子用（多个派发组、两条通道）。</para>
/// </summary>
public abstract class LlmClientContext : IDisposable
{
    private readonly object _httpGate = new();
    private HttpClient? _sharedHttp;
    private int _disposed;

    protected LlmClientContext(HttpClientStrategy httpStrategy)
    {
        if (!Enum.IsDefined(httpStrategy))
            throw new ArgumentOutOfRangeException(nameof(httpStrategy), httpStrategy, null);
        HttpStrategy = httpStrategy;
    }

    public HttpClientStrategy HttpStrategy { get; }

    /// <summary>
    /// 建一个 HttpClient（连同它的 handler 一起归它释放）。只管怎么建：
    /// <see cref="HttpClientStrategy.Shared"/> / <see cref="HttpClientStrategy.Static"/> 时调一次，
    /// <see cref="HttpClientStrategy.PerClient"/> 时每个槽调一次；谁来释放由策略决定。
    /// </summary>
    protected virtual HttpClient CreateHttpClient()
        => throw new NotSupportedException($"{GetType().Name} does not provide an HttpClient.");

    /// <summary>建一个槽。只挂引用，要快；池子按需调用。</summary>
    protected internal abstract LlmClient CreateClient();

    /// <summary>按策略给槽一个 HttpClient。</summary>
    internal HttpClient HttpFor() => HttpStrategy switch
    {
        HttpClientStrategy.Shared or HttpClientStrategy.Static => SharedHttp(),
        HttpClientStrategy.PerClient => Create(),
        _ => throw new NotSupportedException($"{GetType().Name} gives its clients no HttpClient (strategy {HttpStrategy})."),
    };

    /// <summary>槽回收时交还它的 HttpClient：每槽私有的就此释放，共用的、静态的不动。</summary>
    internal void Return(HttpClient http)
    {
        if (HttpStrategy == HttpClientStrategy.PerClient)
            http.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>释放共用的 HttpClient（静态的不动）。子类要额外清理就覆盖，记得调 base。</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && HttpStrategy == HttpClientStrategy.Shared)
            Volatile.Read(ref _sharedHttp)?.Dispose();
    }

    private HttpClient SharedHttp()
    {
        var http = Volatile.Read(ref _sharedHttp);
        if (http is not null)
            return http;

        lock (_httpGate)
            return _sharedHttp ??= Create();
    }

    private HttpClient Create()
        => CreateHttpClient() ?? throw new InvalidOperationException($"{GetType().Name}.CreateHttpClient returned null.");
}
