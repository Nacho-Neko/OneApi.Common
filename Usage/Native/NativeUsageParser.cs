using System.Text;

namespace OneApi.Common.Usage.Native;

/// <summary>
/// 从原生整包 JSON 和 SSE 事件里读用量。这里只做**与协议无关**的三件事：SSE 拆帧、按格式挑读取器、读不出时不抛。
/// 各家的字段名和净/毛口径在 <see cref="NativeUsageReader"/> 的子类里。
///
/// <para><b>读不出不抛。</b>读用量是旁路：读不出已经够糟，再把整条响应打断就是把"账单少算"升级成"请求失败"。
/// 读不出的那一帧交给 <c>onUnreadable</c>，记不记日志由调用方定——共享层不绑日志框架。</para>
/// </summary>
public static class NativeUsageParser
{
    public static void ObserveJson(
        ReadOnlySpan<byte> utf8, NativeUsageFormat format, LlmUsageReading reading, Action<Exception>? onUnreadable = null)
    {
        if (NativeUsageReaders.For(format) is not { } reader)
            return;

        try
        {
            reader.Read(utf8, reading);
        }
        catch (Exception ex)
        {
            onUnreadable?.Invoke(ex);
        }
    }

    /// <summary>一段原始 SSE 文本，可能含多帧；每帧单独读，一帧读不出不影响后面的。</summary>
    public static void ObserveRawSse(
        string raw, NativeUsageFormat format, LlmUsageReading reading, Action<Exception>? onUnreadable = null)
    {
        foreach (var json in ExtractSseJsonPayloads(raw))
            ObserveJson(json, format, reading, onUnreadable);
    }

    private static IEnumerable<byte[]> ExtractSseJsonPayloads(string raw)
    {
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                continue;
            var payload = trimmed[5..].Trim();
            if (payload.Length == 0 || payload == "[DONE]")
                continue;
            if (payload[0] is not ('{' or '['))
                continue;
            yield return Encoding.UTF8.GetBytes(payload);
        }
    }
}
