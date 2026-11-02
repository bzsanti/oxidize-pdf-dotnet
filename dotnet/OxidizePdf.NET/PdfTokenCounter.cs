using System.Text;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Counter used for chunk budgets and reported counts.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TokenCounterKind>))]
public enum TokenCounterKind
{
    /// <summary>Whitespace-separated word estimate; the historical default.</summary>
    WordProxy = 0,
    /// <summary>cl100k_base ordinary-text BPE; special-token spellings count as text.</summary>
    Cl100kBase = 1,
}

/// <summary>An owned count with counter provenance. No native handle is retained.</summary>
public sealed record TokenCountResult(ulong Count, TokenCounterKind Counter)
{
    /// <summary>Whether Count uses cl100k_base rather than a word estimate.</summary>
    public bool IsExact => Counter == TokenCounterKind.Cl100kBase;
    /// <summary>Stable counter identifier; does not include model/chat framing overhead.</summary>
    public string CounterName => Counter == TokenCounterKind.Cl100kBase ? "cl100k_base" : "word-proxy";
}

/// <summary>Counts text using the core counter selected by the caller.</summary>
public static class PdfTokenCounter
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>Discover support in the loaded native library without loading BPE tables.</summary>
    public static bool IsAvailable(TokenCounterKind counter)
    {
        ValidateKind(counter);
        return NativeMethods.oxidize_token_counter_available((byte)counter) != 0;
    }

    /// <summary>Count at most 16 MiB of UTF-8 text. Embedded NUL is preserved.
    /// Unpaired UTF-16 surrogates are rejected. Native builds without tiktoken
    /// throw NotSupportedException for Cl100kBase; no heuristic fallback occurs.</summary>
    public static TokenCountResult Count(string text, TokenCounterKind counter = TokenCounterKind.WordProxy)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateKind(counter);
        if (Utf8.GetByteCount(text) > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(text), "Input exceeds 16 MiB UTF-8.");
        var input = Utf8.GetBytes(text);
        int code = NativeMethods.oxidize_count_tokens(input, (nuint)input.Length, (byte)counter, out var count);
        PdfExtractor.ThrowIfError(code, "Failed to count tokens");
        return new TokenCountResult((ulong)count, counter);
    }

    internal static void ValidateKind(TokenCounterKind counter)
    {
        if (!Enum.IsDefined(counter)) throw new ArgumentOutOfRangeException(nameof(counter));
    }
}
