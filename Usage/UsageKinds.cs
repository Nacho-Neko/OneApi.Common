namespace OneApi.Common.Usage;

/// <summary>
/// 单次上游模型调用的类别常量。一条消息 / 一个回合可能包含多类调用：
/// 主补全、自主唤醒、记忆 RAG 向量化、生图 / 语音 / 视频、agent 工具调起的次级模型。
/// token 计量类与平台计费契约（TavernUsageKinds）对齐。
/// </summary>
public static class UsageKinds
{
    /// <summary>用户回合的主对话补全。</summary>
    public const string Chat = "chat";

    /// <summary>自主唤醒回合的补全（含 agent 工具往返）。</summary>
    public const string Wake = "wake";

    /// <summary>记忆 RAG 的向量化调用（embedding）。</summary>
    public const string Embedding = "embedding";

    /// <summary>图片生成。</summary>
    public const string Image = "image";

    /// <summary>语音合成 / 识别。</summary>
    public const string Audio = "audio";

    /// <summary>视频生成。</summary>
    public const string Video = "video";

    /// <summary>agent 回合中被工具调起的次级模型调用。</summary>
    public const string Tool = "tool";

    /// <summary>
    /// Tavern 回合 commit RPC 对媒体类的聚合 kind（与 <c>TavernUsageKinds.Generation</c> 同源）。
    /// 落库 kind（image/audio/video）经 <see cref="ToTavernCommitKind"/> 折叠后再 commit。
    /// </summary>
    public const string TavernGeneration = "generation";

    /// <summary>该类别的 usage json 是否为 token 形（可汇总 input / output / reasoning）。</summary>
    public static bool IsTokenShaped(string kind) =>
        kind is Chat or Wake or Embedding or Tool;

    /// <summary>媒体生成类（image / audio / video）。</summary>
    public static bool IsMediaKind(string kind) =>
        kind is Image or Audio or Video;

    /// <summary>推导默认计量单位（见 <see cref="UsageUnits.ForKind"/>）。</summary>
    public static string DefaultUnit(string kind) => UsageUnits.ForKind(kind);

    /// <summary>
    /// 落库 / 内部 kind → Tavern commit wire kind。媒体类聚合为 <see cref="TavernGeneration"/>，
    /// token 类（chat/wake/embedding/tool）原样透传。
    /// </summary>
    public static string ToTavernCommitKind(string kind) =>
        IsMediaKind(kind) ? TavernGeneration : kind;
}
