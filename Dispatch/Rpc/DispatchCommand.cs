using MessagePack;
using OneApi.Common.Llm;

namespace Meeko.Contracts.Demux.Dispatch;

/// <summary>
/// LLM 派发命令。调用方（受信内网服务，如 DemuxAi.Edge）已完成鉴权、计费预扣与
/// 模型别名解析：别名 → <see cref="VendorKey"/> + <see cref="VendorModel"/>。
/// </summary>
[MessagePackObject]
public sealed class DispatchCommand
{
    /// <summary>路由 vendor_key（NATS 基础队列组；多池渠道由网关按路由表二次改写）。</summary>
    [Key(0)] public string VendorKey { get; set; } = string.Empty;

    /// <summary>
    /// 渠道真实模型名（路由表的 <c>vendor_model</c>）：多池派发路由与统计的依据，
    /// 并由网关带外转交 provider。协议若把 model 放在 body 里，<see cref="PayloadJson"/>
    /// 中的值与此一致；Gemini 形态的 payload 则没有该字段，本字段是唯一来源。
    /// </summary>
    [Key(1)] public string VendorModel { get; set; } = string.Empty;

    /// <summary>原生协议请求体（UTF-8 JSON）。</summary>
    [Key(2)] public byte[] PayloadJson { get; set; } = [];

    /// <summary>
    /// 调用方协议格式。决定原生直通（NativeFormat 匹配时透传）与 conversationId
    /// 提取器的解析方式。与 <c>TaskEnvelope.CallerFormat</c> 同为枚举：本契约两端
    /// 都是自家服务，没有理由在这一跳退化成自由字符串再解析回来。
    /// <see cref="WireFormat.Unspecified"/> 表示调用方未声明。
    /// </summary>
    [Key(3)] public WireFormat CallerFormat { get; set; }

    /// <summary>
    /// 会话 id 第一层提示（调用方从 <c>x-conversation-id</c> 请求头取得）。
    /// 为空时网关只再看 body 里协议自带的显式会话字段（Anthropic
    /// <c>metadata.user_id</c>），<b>不会</b>从 prompt 内容推导会话身份。
    /// </summary>
    [Key(4)] public string? ConversationIdHint { get; set; }

    /// <summary>调用方请求关联 id（trace id），仅用于日志串联。</summary>
    [Key(5)] public string? RequestId { get; set; }

    /// <summary>
    /// prompt-cache 亲和键的隔离域：调用方身份（Demux 传
    /// <c>AccessTokenResolution.TokenId</c>）。只参与亲和键派生，<b>不进</b>
    /// <c>TaskEnvelope</c>、也永不出现在上游会话字段里。
    ///
    /// <para><b>为什么必须有</b>：无显式会话 id 时亲和键回落到「system + tools 指纹」。
    /// 角色扮演场景的 system 就是社区公共角色卡，同一张卡的所有用户指纹<b>恒等</b>，
    /// 于是全部流量挤向同一账号的少数私有槽位（Claude 每账号仅 2 个），溢出后互相
    /// 覆盖 Redis 映射，结果谁都命中不了。掺入调用方身份把「同卡不同人」拆到不同
    /// 账号，同时保留「同人同卡」的跨轮亲和。</para>
    ///
    /// <para>MessagePack 数组格式下新增末位 Key 双向兼容：旧网关跳过多出来的元素，
    /// 新网关收到旧调用方的命令时该字段为 <c>null</c>（退化为无隔离域）。</para>
    /// </summary>
    [Key(6)] public string? AffinityScope { get; set; }

    /// <summary>
    /// 调用方身份，由 edge 鉴权后填入；内网调用与旧版 edge 为 <c>null</c>。
    ///
    /// <para>紧挨 <see cref="AffinityScope"/> 声明是因为两者都描述「谁在调用」，
    /// 但用途不同、且<b>不能互相替代</b>：AffinityScope 是 prompt-cache 亲和的加盐域
    /// （换成 keyUid 派生会让存量亲和键全部 rehash，白扔一个 PromptCacheTtl 周期的上游
    /// 缓存），本字段则是给抓获规则与遥测关联用的真实身份。</para>
    ///
    /// <para><b>Key 号只能是 7。</b> 本类型是 MessagePack 数组格式，<c>Key(n)</c> 就是
    /// 数组下标而不是标签——插到中间会让其后所有字段整体错位，而错位是<b>静默</b>的：
    /// 未升级的进程照样解析成功，只是把 PayloadJson 当成 CallerFormat 之类。
    /// 追加到末位则双向兼容：旧接收方跳过多出来的元素，新接收方收到旧命令时此项为 null。</para>
    /// </summary>
    [Key(7)] public CallerAccount? CallerAccount { get; set; }
}
