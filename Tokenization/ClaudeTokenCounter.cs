using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace OneApi.Common.Tokenization;

/// <summary>
/// Local Claude 3+/4.x token counter, for the two places a Claude token count has to be
/// produced without the upstream telling us one:
/// <list type="bullet">
///   <item>channels serving Claude models whose upstream reports NO usage at all
///     (Kiro bills in <c>credits</c>), where the per-round output cap is measured in
///     CLAUDE tokens — an o200k count of the same text lands 5–40% lower, so a capped
///     round can measure "under 6000" in o200k units and silently evade cap-based
///     truncation detection;</item>
///   <item>the Demux edge billing a stream the client aborted before the terminal
///     <c>usage</c> frame arrived, where the only evidence left is the text already
///     delivered.</item>
/// </list>
///
/// Anthropic ships no public tokenizer for Claude 3+, so this uses the next best
/// thing: a vocabulary of 38,360 token strings reverse-engineered from Anthropic's
/// <c>count_tokens</c> API (the ctoc project, https://github.com/rohangpta/ctoc),
/// applied with greedy longest-match over UTF-8 bytes. Unknown bytes count as one
/// token each (conservative). Accuracy is ~96% vs the real API on code/prose —
/// materially better than o200k for Claude content, though still an approximation,
/// not Anthropic's authoritative number.
///
/// <para>KNOWN STALENESS, accepted: Claude 4.7 and later (including Fable 5 / Mythos 5)
/// switched to a new tokenizer that yields roughly 30% MORE tokens for the same text
/// (Anthropic's token-counting docs). This vocabulary predates that, so counts for those
/// models run ~30% low. Deliberately left uncorrected rather than guessing a factor —
/// recalibrating against <c>count_tokens</c> is the fix when it becomes worth the effort.
/// Both consumers under-charge slightly as a result, which is the safe direction.</para>
///
/// The trie is built once on first use from an embedded gzipped resource
/// (<c>ClaudeVocab.json.gz</c>, ~113 KB) and is immutable afterwards, so
/// <see cref="Count"/> is thread-safe with no locking on the hot path.
/// </summary>
public static class ClaudeTokenCounter
{
    private static readonly Lazy<Trie> Vocab =
        new(LoadTrie, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Counts the Claude tokens in <paramref name="text"/> via greedy longest-match
    /// against the verified vocabulary. Returns 0 for null/empty input.
    /// </summary>
    public static int Count(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var trie = Vocab.Value;
        var bytes = Encoding.UTF8.GetBytes(text);
        var count = 0;
        var i = 0;
        while (i < bytes.Length)
        {
            var match = trie.LongestMatch(bytes, i);
            i += match > 0 ? match : 1; // unknown byte → single-byte fallback token
            count++;
        }
        return count;
    }

    private static Trie LoadTrie()
    {
        const string resourceName = "OneApi.Common.Tokenization.ClaudeVocab.json.gz";
        using var raw = typeof(ClaudeTokenCounter).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var gz = new GZipStream(raw, CompressionMode.Decompress);

        var tokens = JsonSerializer.Deserialize<string[]>(gz)
            ?? throw new InvalidOperationException("Claude vocab resource deserialized to null.");

        var trie = new Trie();
        foreach (var token in tokens)
            trie.Insert(Encoding.UTF8.GetBytes(token));
        return trie;
    }

    /// <summary>
    /// Byte-level trie (~86K nodes for the full vocab). Nodes are stored as parallel
    /// lists: per-node child map (byte → node index) and terminal flag. Read-only
    /// after construction.
    /// </summary>
    private sealed class Trie
    {
        private readonly List<Dictionary<byte, int>> _children = new(90_000) { new Dictionary<byte, int>() };
        private readonly List<bool> _terminal = new(90_000) { false };

        public void Insert(ReadOnlySpan<byte> token)
        {
            var node = 0;
            foreach (var b in token)
            {
                if (!_children[node].TryGetValue(b, out var next))
                {
                    next = _children.Count;
                    _children[node][b] = next;
                    _children.Add(new Dictionary<byte, int>());
                    _terminal.Add(false);
                }
                node = next;
            }
            _terminal[node] = true;
        }

        /// <summary>Length in bytes of the longest vocab token starting at <paramref name="pos"/>, or 0.</summary>
        public int LongestMatch(ReadOnlySpan<byte> data, int pos)
        {
            var node = 0;
            var best = 0;
            for (var i = pos; i < data.Length; i++)
            {
                if (!_children[node].TryGetValue(data[i], out node)) break;
                if (_terminal[node]) best = i - pos + 1;
            }
            return best;
        }
    }
}
