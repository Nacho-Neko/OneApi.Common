namespace OneApi.Common.Usage;

/// <summary>
/// 一次上报读到的 LLM 产出，各项与 <see cref="LlmOutput"/> 同义；null = 这一次没报这一项。
/// </summary>
public sealed record LlmOutputUpdate(
    long? Text = null,
    long? Reasoning = null,
    long? Image = null,
    long? Audio = null) : IUsageUpdate<LlmOutput>
{
    public LlmOutput Over(LlmOutput? earlier) => new()
    {
        Text      = Text      ?? earlier?.Text      ?? 0,
        Reasoning = Reasoning ?? earlier?.Reasoning ?? 0,
        Image     = Image     ?? earlier?.Image     ?? 0,
        Audio     = Audio     ?? earlier?.Audio     ?? 0,
    };
}
