using MessagePack;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// 给 OneApi 网关的请求分类器用的信号：分类器要看、而网关自己拿不到的东西，由产品 edge（Yomi 等）取好，
/// 填进 <see cref="DispatchCommand.Signals"/>（设计见 OneApi 仓库 <c>src/OneApi.Gateway/request-admission.md</c>）。
/// 分类器在 OneApi，edge 只负责搬运，不做判断。
///
/// <para>现在只有一种信号：这次请求的 HTTP 请求头（<see cref="Headers"/>）。它只在 edge 收到的那次 HTTP 请求里，
/// 到了网关就是一次 gRPC 调用，看不到了。以后 edge 才拿得到的别的信号也加在这个类型里；
/// 请求体里的信号不走这里，网关自己从 <see cref="DispatchCommand.PayloadJson"/> 读。</para>
///
/// <para><b>白名单和取值规则放在这里，不放在 edge。</b> 哪些请求头离开 edge、以什么样子到网关，是这一跳契约的一部分：
/// 写在 edge 里，每个 edge 各抄一份，迟早不一致。</para>
///
/// <para>只收白名单，不做「全收再剔除敏感头」：客户端自定义的鉴权头剔不干净。</para>
///
/// <para><b>这个对象在不在，本身就是信息。</b> 在 = edge 看过客户端的请求头了，<see cref="Headers"/> 里没有的头就是客户端没带；
/// 不在（<see cref="DispatchCommand.Signals"/> 为 null）= edge 没转，网关对客户端一无所知。网关的规则靠这个区分
/// 「没带 User-Agent 的客户端」和「没升级的 edge 发来的全部流量」，所以 edge 看过了就一定要填，哪怕一个头都没有。</para>
///
/// <para>Key 是数组下标。加字段只能追加到下一个空位。</para>
/// </summary>
[MessagePackObject]
public sealed class ClassifierSignals
{
    /// <summary>
    /// 白名单里客户端带了的请求头。键是 <see cref="HeaderNames"/> 里的小写头名，值已按 <see cref="FromHeaders"/> 规整。
    /// 客户端一个都没带时为 null。
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
    ///
    /// <para>总是返回一个对象：客户端一个白名单头都没带，也要让网关知道「edge 看过了、确实没有」。</para>
    /// </summary>
    public static ClassifierSignals FromHeaders(Func<string, IEnumerable<string?>?> read)
    {
        Dictionary<string, string>? headers = null;
        foreach (var name in HeaderNames)
        {
            var value = Normalize(name, read(name));
            if (value is not null)
                (headers ??= new(StringComparer.Ordinal))[name] = value;
        }

        return new ClassifierSignals { Headers = headers };
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
