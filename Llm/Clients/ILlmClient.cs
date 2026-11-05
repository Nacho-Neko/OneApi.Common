namespace OneApi.Common.Llm.Clients;

/// <summary>派发槽的状态。</summary>
public enum LlmClientState
{
    /// <summary>空闲，可以占用。</summary>
    Idle,

    /// <summary>被派发占住，执行还没开始。</summary>
    Reserved,

    /// <summary>正在执行。</summary>
    Processing,

    /// <summary>执行完了，冷却中，冷却完回到空闲。</summary>
    CoolingDown,

    /// <summary>被池子回收，不再接活。</summary>
    Retired,
}

/// <summary>
/// 一个派发槽：同一时刻最多处理一个请求。状态由它自己推进——占用、执行、冷却、回到空闲都在这里，
/// 汇总方扫一遍所有槽就知道哪些在执行，不另记数。
/// </summary>
public interface ILlmClient : IDisposable
{
    LlmClientState State { get; }

    /// <summary>被占住或正在执行（<see cref="LlmClientState.Reserved"/> / <see cref="LlmClientState.Processing"/>）。</summary>
    bool IsProcessing { get; }

    /// <summary>空闲才占得到；两个派发方同时来只有一个成功。</summary>
    bool TryReserve();

    /// <summary>占住了但不派了：直接回到空闲，不冷却。</summary>
    void Release();

    /// <summary>
    /// 在这个槽上执行 <paramref name="work"/>（必须先 <see cref="TryReserve"/>）。执行完按池子的设置冷却，再回到空闲；
    /// 执行抛的异常照原样抛给调用方。<paramref name="work"/> 拿到的令牌只由 <see cref="Cancel"/> 触发。
    /// </summary>
    Task RunAsync(Func<CancellationToken, Task> work);

    /// <summary>取消正在执行的任务（通知 <see cref="RunAsync"/> 交给它的令牌）。没在执行时什么都不做。</summary>
    void Cancel();
}
