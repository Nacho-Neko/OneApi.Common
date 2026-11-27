using MessagePack;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// 终端客户端留下的、用来分辨它是什么客户端的痕迹。产品 edge（Yomi 等）从客户端的 HTTP 请求里取出来，
/// 填进 <see cref="DispatchCommand.Client"/>；OneApi 网关据此给请求分类（设计见 OneApi 仓库
/// <c>src/OneApi.Gateway/request-admission.md</c>）。分类器在 OneApi，edge 只负责搬运。
///
/// <para><b>白名单和取值规则放在这里，不放在 edge。</b> 哪些请求头离开 edge、以什么样子到网关，是这一跳契约的一部分：
/// 写在 edge 里，每个 edge 各抄一份，迟早不一致。</para>
///
/// <para>只收白名单，不做「全收再剔除敏感头」：客户端自定义的鉴权头剔不干净。</para>
///
/// <para>Key 是数组下标。加字段只能追加到下一个空位。</para>
/// </summary>
[MessagePackObject]
public sealed class ClientSignals
{
    /// <summary>
    /// 白名单里客户端带了的请求头。键是 <see cref="HeaderNames"/> 里的小写头名，值已按 <see cref="FromHeaders"/> 规整。
    /// 一个都没带时整个 <see cref="ClientSignals"/> 为 null，不会出现空字典。
    /// </summary>
    [Key(0)] public Dictionary<string, string>? Headers { get; set; }

    /// <summary>头名一律小写。</summary>
    public static readonly string[] HeaderNames =
    [
        "user-agent",
        // 浏览器发出的跨域请求一定带它，网页版客户端靠它认出是哪个站点。
        "origin",
        "referer",
        // Android WebView 可能带上 App 包名：小手机的 UA 是系统默认的，认不出是哪一款。
        "x-requested-with",
        // OpenRouter 约定的应用名和应用地址，有些客户端照着带。
        "x-title",
        "http-referer",
        // Claude Code 的进程形态（cli / cli-bg）和它必带的 beta 标。
        "x-app",
        "anthropic-beta",
        // Codex CLI 的 originator（codex_cli_rs）。
        "originator",
        // OpenAI / Anthropic 官方 SDK 自报的语言和运行时：区分直接用 SDK 写的程序和成品客户端。
        "x-stainless-lang",
        "x-stainless-runtime",
        "x-stainless-package-version",
    ];

    /// <summary>单个头的值最多留这么长。分类只需要开头一段，超长的多半是客户端乱塞的东西。</summary>
    public const int MaxValueLength = 256;

    /// <summary>
    /// 从客户端请求头里取出白名单内的那几个。<paramref name="read"/> 按头名返回客户端带的全部值，
    /// 大小写不敏感由调用方的头集合保证（ASP.NET Core 的 <c>IHeaderDictionary</c> 就是）。
    ///
    /// <para>同名多份用 <c>", "</c> 拼起来，和 HTTP 合并同名头的写法一样；空值不收。
    /// <c>referer</c> / <c>http-referer</c> 去掉查询串和片段：页面地址里常带令牌、会话 id，
    /// 不能跟着进遥测，认站点也用不着。</para>
    /// </summary>
    public static ClientSignals? FromHeaders(Func<string, IEnumerable<string?>?> read)
    {
        Dictionary<string, string>? headers = null;
        foreach (var name in HeaderNames)
        {
            var value = Normalize(name, read(name));
            if (value is not null)
                (headers ??= new(StringComparer.Ordinal))[name] = value;
        }

        return headers is null ? null : new ClientSignals { Headers = headers };
    }

    private static string? Normalize(string name, IEnumerable<string?>? values)
    {
        if (values is null)
            return null;

        var value = string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
        if (value.Length == 0)
            return null;

        if (name is "referer" or "http-referer")
        {
            var cut = value.IndexOfAny(['?', '#']);
            if (cut >= 0)
                value = value[..cut];
            if (value.Length == 0)
                return null;
        }

        return value.Length <= MaxValueLength ? value : value[..MaxValueLength];
    }
}
