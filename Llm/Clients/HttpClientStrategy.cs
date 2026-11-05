namespace OneApi.Common.Llm.Clients;

/// <summary>
/// 公共对象给槽 HttpClient 的策略，创建 <see cref="LlmClientContext"/> 时指定。
/// 生命周期全由策略决定，<see cref="LlmClientContext.CreateHttpClient"/> 只管怎么建。
/// </summary>
public enum HttpClientStrategy
{
    /// <summary>槽不走 HTTP（自己管连接，或者不调上游）。</summary>
    None,

    /// <summary>账号的所有槽共用一个：第一次有槽要用时建，随公共对象释放。</summary>
    Shared,

    /// <summary>每个槽一个：槽第一次要用时建，随槽回收。</summary>
    PerClient,

    /// <summary>所有槽共用一个外部的静态实例：谁都不释放它。</summary>
    Static,
}
