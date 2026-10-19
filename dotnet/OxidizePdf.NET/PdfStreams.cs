using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Raw encoded streams and explicit document-aware bounded decoding.
/// Page and content indexes are zero-based. Inputs/settings are snapshotted before scheduling.
/// Cancellation prevents queued work; it cannot interrupt an active native call.</summary>
public static class PdfStreams
{
    /// <summary>Reads an indirect stream without decoding its payload or resolving filter metadata.</summary>
    public static async Task<PdfRawStream> ReadAsync(byte[] pdf, PdfIndirectObjectId objectId,
        PdfStreamOptions? options = null, PdfParserOptions? parserOptions = null, CancellationToken ct = default)
        => (await CallAsync<PdfRawStream[]>(pdf, "Read", objectId, null, null, options, parserOptions, ct).ConfigureAwait(false)).Single();

    /// <summary>Reads page Contents in declared order; absent/null Contents yields an empty array.
    /// Encoded and metadata budgets apply to the whole returned array.</summary>
    public static Task<PdfRawStream[]> ReadPageAsync(byte[] pdf, uint pageIndex,
        PdfStreamOptions? options = null, PdfParserOptions? parserOptions = null, CancellationToken ct = default)
        => CallAsync<PdfRawStream[]>(pdf, "ReadPage", null, pageIndex, null, options, parserOptions, ct);

    /// <summary>Decodes an indirect stream with the original document's DecodeParms resolution.
    /// Strict decoding rejects nonempty payloads with an empty Filter array because core 5.4.2
    /// would otherwise silently discard their data.</summary>
    public static Task<PdfDecodedStream> DecodeAsync(byte[] pdf, PdfIndirectObjectId objectId,
        PdfStreamOptions? options = null, PdfParserOptions? parserOptions = null, CancellationToken ct = default)
        => CallAsync<PdfDecodedStream>(pdf, "Decode", objectId, null, null, options, parserOptions, ct);

    /// <summary>Decodes one page content stream, including streams without indirect identity.</summary>
    public static Task<PdfDecodedStream> DecodePageContentAsync(byte[] pdf, uint pageIndex, uint contentsIndex,
        PdfStreamOptions? options = null, PdfParserOptions? parserOptions = null, CancellationToken ct = default)
        => CallAsync<PdfDecodedStream>(pdf, "Decode", null, pageIndex, contentsIndex, options, parserOptions, ct);

    private static Task<T> CallAsync<T>(byte[] pdf, string action, PdfIndirectObjectId? objectId,
        uint? pageIndex, uint? contentsIndex, PdfStreamOptions? options, PdfParserOptions? parserOptions, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        if (pdf.Length == 0 || pdf.Length > 100 * 1024 * 1024)
            throw new ArgumentException("PDF must contain between 1 byte and 100 MiB", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        var request = JsonSerializer.Serialize(new
        {
            action,
            object_id = objectId is { } id ? new uint[] { id.ObjectNumber, id.GenerationNumber } : null,
            page_index = pageIndex,
            contents_index = contentsIndex,
            options = options ?? new PdfStreamOptions(),
            parser_options = parserOptions ?? new PdfParserOptions()
        });
        var snapshot = (byte[])pdf.Clone();
        return Task.Run(() => Call<T>(snapshot, request), ct);
    }

    private static T Call<T>(byte[] pdf, string request)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr output = IntPtr.Zero;
        try
        {
            var status = StreamsNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out output);
            if (status != 0) throw new PdfStreamException(PdfStreamErrorKind.Other, NativeMethods.GetLastError() ?? "Stream operation failed");
            using var envelope = JsonDocument.Parse(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfStreamException(PdfStreamErrorKind.Other, "Missing stream response"));
            if (envelope.RootElement.TryGetProperty("error", out var error))
                throw new PdfStreamException((PdfStreamErrorKind)error.GetProperty("kind").GetInt32(), error.GetProperty("message").GetString()!);
            return envelope.RootElement.GetProperty("result").Deserialize<T>()
                ?? throw new PdfStreamException(PdfStreamErrorKind.Other, "Invalid stream response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            pinned.Free();
        }
    }

    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_streams", CallingConvention = CallingConvention.Cdecl)]
    private static extern int StreamsNative(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string request, out IntPtr output);
}

/// <summary>Budgets apply after core parsing, not to the parser's total allocations or elapsed time.
/// All byte budgets accept zero; exceeding a budget fails without returning partial success.</summary>
public sealed class PdfStreamOptions
{
    /// <summary>Maximum encoded payload bytes, aggregated for ReadPageAsync.</summary>
    public ulong MaxEncodedBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Maximum output at each decoding stage; core also enforces its own hard limits.</summary>
    public ulong MaxDecodedBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Explicitly permit Flate recovery with diagnostics. Only affects decode calls.</summary>
    public bool AllowRecovery { get; set; } = false;
    /// <summary>Maximum content streams returned by ReadPageAsync.</summary>
    public uint MaxStreams { get; set; } = 1024;
    /// <summary>Maximum indirect dereferences per selector chain (0–128).</summary>
    public uint MaxReferenceDepth { get; set; } = 32;
    /// <summary>Maximum nesting below a Filter/DecodeParms root (0–16).</summary>
    public uint MaxMetadataDepth { get; set; } = 16;
    /// <summary>Maximum reported metadata values, aggregated across both keys and all streams.</summary>
    public uint MaxMetadataNodes { get; set; } = 4096;
    /// <summary>Maximum UTF-8 names/dictionary keys and string bytes in reported metadata.
    /// Excludes JSON encoding and numeric/reference values.</summary>
    public ulong MaxMetadataBytes { get; set; } = 64 * 1024;
}

/// <summary>A declared PDF metadata value. References remain unresolved.</summary>
public sealed class PdfStreamValue
{
    /// <summary>Null, Boolean, Integer, Real, Name, Bytes, Array, Dictionary or Reference.</summary>
    [JsonPropertyName("type")] public string Type { get; init; } = "Null";
    /// <summary>Arrays retain order; dictionaries map names to typed values; Bytes is base64,
    /// Reference is [object number, generation]. Missing payload represents PDF null.</summary>
    [JsonPropertyName("value")] public JsonElement? Value { get; init; }
}

/// <summary>An owned copy of a parser's encoded stream, separate from decoded data.</summary>
public sealed class PdfRawStream
{
    /// <summary>Encoded payload as returned by core; not a physical file slice.</summary>
    [JsonPropertyName("data")] public byte[] Data { get; init; } = [];
    /// <summary>Final resolved stream identity, or null for a direct stream.</summary>
    [JsonPropertyName("object_id")] public PdfIndirectObjectId? ObjectId { get; init; }
    /// <summary>Zero-based position in page Contents, or null for object selection.</summary>
    [JsonPropertyName("contents_index")] public ulong? ContentsIndex { get; init; }
    /// <summary>Declared Filter name/ordered array; null means absent. PDF null is a typed value.</summary>
    [JsonPropertyName("filter")] public PdfStreamValue? Filter { get; init; }
    /// <summary>Declared DecodeParms dictionary/ordered array/reference, preserving null slots.</summary>
    [JsonPropertyName("decode_parameters")] public PdfStreamValue? DecodeParameters { get; init; }
}

/// <summary>Decoded bytes plus every recovery diagnostic; diagnostics must be inspected.</summary>
public sealed class PdfDecodedStream
{
    /// <summary>Decoded output; may be incomplete or unverified only when diagnostics say so.</summary>
    [JsonPropertyName("data")] public byte[] Data { get; init; } = [];
    /// <summary>Final resolved stream identity, if available.</summary>
    [JsonPropertyName("object_id")] public PdfIndirectObjectId? ObjectId { get; init; }
    /// <summary>Zero-based page Contents position, if selected by page.</summary>
    [JsonPropertyName("contents_index")] public ulong? ContentsIndex { get; init; }
    /// <summary>Ordered per-filter integrity/completion limitations. Empty does not certify the PDF.</summary>
    [JsonPropertyName("diagnostics")] public PdfStreamRecoveryDiagnostic[] Diagnostics { get; init; } = [];
    /// <summary>True when all filter stages finished without recovery; not a whole-document validity claim.</summary>
    public bool DecodedWithoutRecovery => Diagnostics.Length == 0;
}

/// <summary>A core Flate recovery diagnostic.</summary>
public sealed class PdfStreamRecoveryDiagnostic
{
    /// <summary>Zero-based position in the filter chain.</summary>
    [JsonPropertyName("filter_index")] public ulong FilterIndex { get; init; }
    /// <summary>Unverified integrity or incomplete output.</summary>
    [JsonPropertyName("kind")] public PdfFlateRecoveryKind Kind { get; init; }
    /// <summary>Original decoder diagnostic.</summary>
    [JsonPropertyName("error")] public string Error { get; init; } = "";
}

/// <summary>Preserves core recovery error classification; strict decoding has no finer typed classification.</summary>
public enum PdfStreamErrorKind
{
    /// <summary>Recovery found no usable Flate output.</summary>
    InvalidFlate,
    /// <summary>Core Flate recovery resource limit or wrapper inspection/encoded budget.</summary>
    ResourceLimit,
    /// <summary>Parser, selector, metadata, predictor or other core recovery failure.
    /// Core classifies non-Flate recovery limit errors here.</summary>
    Other,
    /// <summary>Strict bounded decoding failed, including corruption, unsupported filter or limit.</summary>
    DecodeError
}

/// <summary>Typed stream operation failure. No failed operation returns partial successful data.</summary>
public sealed class PdfStreamException : Exception
{
    /// <summary>Core/wrapper failure classification.</summary>
    public PdfStreamErrorKind Kind { get; }
    /// <summary>Creates a stream failure.</summary>
    public PdfStreamException(PdfStreamErrorKind kind, string message) : base(message) => Kind = kind;
}
