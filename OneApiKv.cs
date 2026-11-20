namespace OneApi.Common;

/// <summary>
/// OneApi 放在 NATS 上的「库」：一个 JetStream KV 桶，名字写死，不开放配置。
/// <para>
/// 当成数据库来看：桶是库，键的第一段是表。NATS 的桶是平的，桶下面不能再套桶；但桶里的键按点号分层，
/// 订阅和授权都可以只对着某个前缀——<c>ratecard.&gt;</c> 就是「价格表这张表里的全部」。
/// 所以不为每样数据各开一个桶（<c>oneapi-pricing</c>、<c>oneapi-xxx</c>……），都放在这一个库里，各占一个前缀。
/// 现在只有价格表（<see cref="Pricing.ModelPriceKeys"/>）；OneApi 别的要落盘、要推给订阅者的设置以后加在这里。
/// </para>
/// <para>
/// 这个库里的东西都是「不常改、不能自己消失」的：桶落盘、永不过期、每个键只留最新一条。
/// 账号状态不在这里——那些桶放内存、到点过期、由号池后台一直重发，是另一种东西。
/// </para>
/// <para>
/// 不开放配置的原因：名字一旦可配，写的一方和读的一方就可能配得不一样，读的那头收到的是一张空表，而且看不出为什么。
/// 开发、演练的环境不该和正式环境连同一个 NATS；真连上了，价格表有「主人」那道闸挡着（<see cref="Pricing.ModelPriceKeys.OwnerKey"/>）。
/// </para>
/// </summary>
public static class OneApiKv
{
    /// <summary>库名（桶名）。</summary>
    public const string Bucket = "oneapi";
}
