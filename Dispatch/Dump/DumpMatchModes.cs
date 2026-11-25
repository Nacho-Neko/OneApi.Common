namespace OneApi.Common.Dispatch.Dump;

/// <summary>
/// 时间窗规则之间怎么组合（<see cref="DumpPolicyDoc.MatchMode"/>）。**wire 契约**，控制台和网关认同一组值。
///
/// <para><see cref="All"/> 是为了「只抓某个渠道的某种错误」：渠道规则在发请求前判、错误码规则在结束后判，
/// 「或」只能抓到两者的并集。同一种条件要多选，写在一条规则的逗号分隔值里，那仍是「或」。</para>
/// </summary>
public static class DumpMatchModes
{
    /// <summary>任意一条规则命中就抓（缺省）。</summary>
    public const string Any = "any";

    /// <summary>每一条启用的规则都命中才抓。</summary>
    public const string All = "all";

    public static bool IsKnown(string? mode) => mode is Any or All;
}
