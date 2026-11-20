using MagicOnion;

namespace OneApi.Common.Pricing.Relay;

/// <summary>
/// 各产品的业务向平台要价格表用的接口。平台这边由 Bff 实现，挂在它的内部 RPC 端口上。
/// <para>
/// 价格表的主人是 OneApi 的号池后台，发在 OneApi 的 NATS 上（<see cref="ModelPriceKeys"/>）。
/// 产品不直接连那个 NATS：那是 OneApi 内部的总线，每加一个产品就要在上面多开一个账号、多一份必填的配置。
/// 平台这边只有 Bff 连着它，订阅价格表，再原样转给产品——产品本来就只连平台。
/// </para>
/// <para>
/// <b>这个接口在两个仓库里各有一份</b>：这里（产品引用 OneApi.Common）和平台仓库的 Bff 里
/// （平台不引用 OneApi.Common）。MagicOnion 按接口名和方法名对，两边必须一字不差；
/// 参数和返回值只用字符串和字节数组，就是为了没有第二个要对的类型。改这里就要同时改那边。
/// </para>
/// </summary>
public interface IModelPriceRelayService : IService<IModelPriceRelayService>
{
    /// <summary>
    /// 取平台手上现在的价格表。
    /// </summary>
    /// <param name="knownFingerprint">
    /// 调用方手上那一份的指纹（上一次回来的 <see cref="ModelPriceRelayEnvelope.Fingerprint"/>）；第一次要传空串，不传 null。
    /// </param>
    /// <returns>
    /// 和 <paramref name="knownFingerprint"/> 是同一份：空数组。不是：<see cref="ModelPriceRelayEnvelope.Pack"/> 打出来的整张表。
    /// 平台自己还没从 NATS 收到过（刚起来、NATS 不通）时不回空表，报 <c>Unavailable</c>——
    /// 「还不知道」和「一行都没有」不是一回事，后者产品会当真。
    /// </returns>
    UnaryResult<byte[]> GetAsync(string knownFingerprint);
}

/// <summary>产品和平台之间关于价格表的几个约定值。</summary>
public static class ModelPriceRelay
{
    /// <summary>
    /// 「去平台把价格表取回来」这个定时任务的键。要价格表的产品在代码里声明它（随 Consul 注册发布），
    /// 两处会来触发：平台的 Bff 在价格表变了的那一刻（它按这个键在 Consul 上找到要通知谁），
    /// 和平台的 Jobs 按 <see cref="RefreshCron"/>。
    /// <para>
    /// 借定时任务的入口，是因为它是现成的「平台调进产品」的路：产品已经实现了、凭据已经配了，
    /// 不用为「通知一声」再加一种声明、一个接口。Bff 里按同一个字符串找，改这里就要同时改那边。
    /// </para>
    /// </summary>
    public const string RefreshTaskKey = "model-prices-refresh";

    /// <summary>
    /// Jobs 兜底触发的周期：每小时一次。知道价格变了靠的是 Bff 当场通知，不是这个；
    /// 它补的是通知没送到（那一刻产品正好够不着）和上一次重算没成（<see cref="ModelPriceTableFeed"/> 会在下一次再试）。
    /// 表没变的时候这一趟只是一次空的往返。
    /// </summary>
    public const string RefreshCron = "23 * * * *";
}
