using MessagePack;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// 调用方身份。由 edge（Demux / Tavern / ToApi）在鉴权完成后填入
/// <see cref="DispatchCommand.CallerAccount"/>，网关只读不改。
///
/// <para><b>为什么是对象而不是几个平铺字段。</b> 这四项是一个整体：要么整条链路都不知道
/// 调用方是谁（内网调用、旧版 edge），要么四项一起到位。摊平成 4 个可空标量的话，
/// 「AccountUid 有值但 AccountType 为空」这种半截状态在类型上就是合法的，接收侧每处
/// 都得重新判断一遍「这次到底算不算认识调用方」。包成一个可空对象，判空一次就够了。</para>
///
/// <para><b>只放 id，不放名字。</b> 邮箱 / 账户名 / 组织名一律不进这里——它们会改，
/// 写进遥测就成了与现值冲突的历史快照，而且遥测的保留期靠 TTL，PII 进去就没法单独删。
/// 需要显示名的地方按 id 向 Keystone 关联（见 dump-capture.md §2.1 的判据）。</para>
/// </summary>
[MessagePackObject]
public sealed class CallerAccount
{
    /// <summary>
    /// 账号主体。Account 域是 AccountUid，Staff 域是 StaffUid
    /// （与 <c>ICurrentAuth.PrincipalUid</c> 同义）。对象存在时此项必有值。
    /// </summary>
    [Key(0)] public long AccountUid { get; set; }

    /// <summary>IAM 子账号 UID。个人账号与 API Key 调用没有这一层，为 null。</summary>
    [Key(1)] public long? IamUserUid { get; set; }

    /// <summary>
    /// 发起调用的那把 API Key。LLM 派发这条路是 sk- 认证的，
    /// 同一账号下多把 key 只能靠它区分——「某客户的某个应用在报错」是最常见的排查起点。
    /// </summary>
    [Key(2)] public long? KeyUid { get; set; }

    /// <summary><c>personal</c> | <c>organization</c>。</summary>
    [Key(3)] public string? AccountType { get; set; }
}
