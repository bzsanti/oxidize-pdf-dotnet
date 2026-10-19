using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Bounded font decoding and Adobe collection lookup through the native core.</summary>
public static class PdfFonts
{
    /// <summary>Inspect an exact font resource on a zero-based page. characterCodes
    /// contains PDF string bytes, not Unicode, CIDs or glyph IDs. cids requests
    /// independent vertical advances, even for a horizontal font. Returned arrays,
    /// dictionaries and strings are owned by the caller. Inputs/options are
    /// snapshotted before scheduling. Cancellation cannot interrupt native execution.</summary>
    public static Task<PdfFontInspection> InspectAsync(byte[] pdf, uint pageIndex, string resourceName,
        byte[]? characterCodes = null, IReadOnlyList<uint>? cids = null,
        PdfFontInspectionOptions? options = null, PdfParserOptions? parserOptions = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(resourceName);
        if (pdf.Length == 0 || pdf.Length > 100 * 1024 * 1024)
            throw new ArgumentException("PDF must contain 1 byte to 100 MiB", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        options ??= new();
        options.Validate();
        if (Encoding.UTF8.GetByteCount(resourceName) > 1024
            || (characterCodes?.Length ?? 0) > options.MaxCodeBytes || (cids?.Count ?? 0) > options.MaxCids)
            throw new PdfFontException(PdfFontError.LimitExceeded, "Font query exceeds name/code/CID budget");
        string request = JsonSerializer.Serialize(new
        {
            action = "Inspect",
            page_index = pageIndex,
            resource_name = resourceName,
            character_codes = Convert.ToBase64String(characterCodes ?? []),
            cids = cids?.ToArray() ?? [],
            options,
            parser_options = parserOptions ?? new PdfParserOptions()
        });
        var snapshot = (byte[])pdf.Clone();
        return Task.Run(() => Call<PdfFontInspection>(snapshot, request), ct);
    }

    /// <summary>Look up up to 4096 CIDs in Adobe CNS1, GB1, Japan1, Korea1 or KR.
    /// Ordering is case-sensitive. Unknown collections return Supported=false;
    /// missing CIDs (including .notdef CID 0) return null Unicode. Full sequences
    /// preserve variation selectors and supplementary scalars. This does not
    /// decode PDF character codes or consult a font's authoritative ToUnicode.</summary>
    public static PdfCidCollectionLookup LookupCollection(string ordering, IReadOnlyList<ushort> cids)
    {
        ArgumentNullException.ThrowIfNull(ordering);
        ArgumentNullException.ThrowIfNull(cids);
        if (Encoding.UTF8.GetByteCount(ordering) > 64 || cids.Count > 4096)
            throw new PdfFontException(PdfFontError.LimitExceeded, "Collection lookup exceeds bounds");
        return Call<PdfCidCollectionLookup>([], JsonSerializer.Serialize(new { action = "Collection", ordering, cids = cids.ToArray() }));
    }

    private static T Call<T>(byte[] pdf, string request)
    {
        int code = NativeMethods.oxidize_font_inspect(pdf, (nuint)pdf.Length, request, out var output);
        try
        {
            if (code != 0) throw new PdfFontException(PdfFontError.NativeFailure, NativeMethods.GetLastError() ?? "Font inspection failed");
            using var envelope = JsonDocument.Parse(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfFontException(PdfFontError.NativeFailure, "Missing font response"));
            if (envelope.RootElement.TryGetProperty("error", out var error))
                throw new PdfFontException((PdfFontError)error.GetProperty("kind").GetByte(), error.GetProperty("message").GetString()!);
            return envelope.RootElement.GetProperty("result").Deserialize<T>()
                ?? throw new PdfFontException(PdfFontError.NativeFailure, "Invalid font response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
        }
    }
}

/// <summary>Limits for requested decoding and compact vertical tables. These are
/// not a total budget for PDF parsing, embedded-font loading or core CMap resolution.</summary>
public sealed class PdfFontInspectionOptions
{
    /// <summary>Maximum supplied PDF character-code bytes; default 4096, maximum 65536.</summary>
    public int MaxCodeBytes { get; set; } = 4096;
    /// <summary>Maximum requested CID advances; default/maximum 4096.</summary>
    public int MaxCids { get; set; } = 4096;
    /// <summary>Maximum W2 overrides/ranges processed, including duplicates;
    /// default 4096, maximum 65536. Ranges are never expanded.</summary>
    public int MaxMetricEntries { get; set; } = 4096;
    /// <summary>Conservative vertical range-lookup work: (code bytes + requested
    /// CIDs) * (range count + 1). Default 1,000,000, maximum 16,777,216.
    /// This does not count internal core encoding/ToUnicode lookups.</summary>
    public int MaxLookupWork { get; set; } = 1_000_000;
    /// <summary>Maximum UTF-8 bytes in decoded Unicode results, checked after core
    /// decoding; default 1 MiB, maximum 16 MiB. This is a response limit.</summary>
    public int MaxUnicodeBytes { get; set; } = 1024 * 1024;

    internal void Validate()
    {
        if (MaxCodeBytes is < 0 or > 65536 || MaxCids is < 0 or > 4096
            || MaxMetricEntries is < 0 or > 65536 || MaxLookupWork is < 0 or > 16_777_216
            || MaxUnicodeBytes is < 0 or > 16_777_216)
            throw new ArgumentOutOfRangeException(nameof(PdfFontInspectionOptions), "Font limits are outside supported bounds");
    }
}

/// <summary>Writing direction selected by the core font encoding.</summary>
public enum PdfFontWritingMode
{
    /// <summary>Advance in horizontal text direction.</summary>
    Horizontal,
    /// <summary>Advance in vertical text direction.</summary>
    Vertical,
}

/// <summary>One exact code and its independently resolved identities.</summary>
public sealed class PdfDecodedGlyph
{
    /// <summary>Exact consumed PDF character-code bytes.</summary>
    [JsonPropertyName("source_code")] public byte[] SourceCode { get; init; } = [];
    /// <summary>Composite character identifier, or null for simple/unmapped codes.</summary>
    [JsonPropertyName("cid")] public uint? Cid { get; init; }
    /// <summary>GID from the font mapping, or null when unavailable; zero is a valid value.</summary>
    [JsonPropertyName("glyph_id")] public ushort? GlyphId { get; init; }
    /// <summary>Complete Unicode sequence, or null when unmapped. ToUnicode is authoritative.</summary>
    [JsonPropertyName("unicode")] public string? Unicode { get; init; }
    /// <summary>Effective horizontal/vertical pen advance in glyph units (1/1000 em).</summary>
    [JsonPropertyName("advance")] public double Advance { get; init; }
}

/// <summary>A compact inclusive CID range with a uniform vertical advance.</summary>
public sealed class PdfCidAdvanceRange
{
    /// <summary>First CID (inclusive).</summary>
    [JsonPropertyName("first")] public uint First { get; init; }
    /// <summary>Last CID (inclusive).</summary>
    [JsonPropertyName("last")] public uint Last { get; init; }
    /// <summary>Vertical pen advance in 1/1000 em.</summary>
    [JsonPropertyName("advance")] public double Advance { get; init; }
}

/// <summary>W2/DW2 pen advances; drawing-origin coordinates are not advances.
/// Explicit overrides take precedence, then the last matching range, then default.</summary>
public sealed class PdfCidVerticalMetrics
{
    /// <summary>DW2 second number, or -1000 when absent.</summary>
    [JsonPropertyName("default_advance")] public double DefaultAdvance { get; init; }
    /// <summary>Per-CID overrides; the last duplicate wins.</summary>
    [JsonPropertyName("overrides")] public Dictionary<uint, double> Overrides { get; init; } = new();
    /// <summary>Compact ranges in declaration order.</summary>
    [JsonPropertyName("ranges")] public List<PdfCidAdvanceRange> Ranges { get; init; } = [];
}

/// <summary>One requested CID's vertical pen advance.</summary>
public sealed class PdfCidAdvance
{
    /// <summary>Requested CID, not a source character code or GID.</summary>
    [JsonPropertyName("cid")] public uint Cid { get; init; }
    /// <summary>Advance, or null for a simple font without CID vertical metrics.</summary>
    [JsonPropertyName("advance")] public double? Advance { get; init; }
}

/// <summary>Owned font inspection results; no native handles escape.</summary>
public sealed class PdfFontInspection
{
    /// <summary>Exact page resource name.</summary>
    [JsonPropertyName("resource_name")] public string ResourceName { get; init; } = string.Empty;
    /// <summary>Concrete core subtype (type1, true_type, cid_font_type0, cid_font_type2, type3).</summary>
    [JsonPropertyName("subtype")] public string Subtype { get; init; } = string.Empty;
    /// <summary>Resolved writing direction.</summary>
    [JsonPropertyName("writing_mode")] public PdfFontWritingMode WritingMode { get; init; }
    /// <summary>Glyphs in supplied code order; empty when no codes were requested.</summary>
    [JsonPropertyName("glyphs")] public List<PdfDecodedGlyph> Glyphs { get; init; } = [];
    /// <summary>Declared CIDSystemInfo. This does not certify that the core uses
    /// this collection: an incompatible Encoding or explicit ToUnicode can override fallback.</summary>
    [JsonPropertyName("cid_system_info")] public PdfCidSystemInfo? CidSystemInfo { get; init; }
    /// <summary>Compact vertical tables for CID descendants; null for simple fonts.</summary>
    [JsonPropertyName("vertical_metrics")] public PdfCidVerticalMetrics? VerticalMetrics { get; init; }
    /// <summary>Requested CID advances in supplied order, including duplicates.</summary>
    [JsonPropertyName("vertical_advances")] public List<PdfCidAdvance> VerticalAdvances { get; init; } = [];
}

/// <summary>Declared descendant-font collection metadata, distinct from effective Unicode mapping.</summary>
public sealed class PdfCidSystemInfo
{
    /// <summary>Registry text, or null when absent/not a string.</summary>
    [JsonPropertyName("registry")] public string? Registry { get; init; }
    /// <summary>Ordering text, or null when absent/not a string.</summary>
    [JsonPropertyName("ordering")] public string? Ordering { get; init; }
    /// <summary>Declared supplement, or null when absent/not an integer.</summary>
    [JsonPropertyName("supplement")] public long? Supplement { get; init; }
}

/// <summary>Collection fallback lookup; separate from font-specific ToUnicode.</summary>
public sealed class PdfCidCollectionLookup
{
    /// <summary>Exact requested Adobe ordering.</summary>
    [JsonPropertyName("ordering")] public string Ordering { get; init; } = string.Empty;
    /// <summary>Whether the pinned core supports this collection.</summary>
    [JsonPropertyName("supported")] public bool Supported { get; init; }
    /// <summary>Mappings in requested order, retaining duplicates and missing results.</summary>
    [JsonPropertyName("mappings")] public List<PdfCidUnicodeMapping> Mappings { get; init; } = [];
}

/// <summary>One Adobe collection CID mapping.</summary>
public sealed class PdfCidUnicodeMapping
{
    /// <summary>Requested collection CID.</summary>
    [JsonPropertyName("cid")] public ushort Cid { get; init; }
    /// <summary>Full sequence, or null for an unmapped CID.</summary>
    [JsonPropertyName("unicode")] public string? Unicode { get; init; }
    /// <summary>Unicode scalar value only for a single-scalar mapping; null for missing/multi-scalar mappings.</summary>
    [JsonPropertyName("unicode_scalar")] public uint? UnicodeScalar { get; init; }
}

/// <summary>Typed native font inspection failure.</summary>
public enum PdfFontError
{
    /// <summary>Invalid request/options.</summary>
    InvalidInput,
    /// <summary>Malformed PDF/font, unsupported encoding, page or truncated code.</summary>
    InvalidPdf,
    /// <summary>Requested font resource is absent.</summary>
    MissingFont,
    /// <summary>Requested work or result exceeds a limit.</summary>
    LimitExceeded,
    /// <summary>Unexpected ABI/native failure.</summary>
    NativeFailure,
}

/// <summary>Typed font failure, compatible with existing extraction exception handlers.</summary>
public sealed class PdfFontException : PdfExtractionException
{
    /// <summary>Failure category.</summary>
    public PdfFontError Error { get; }
    internal PdfFontException(PdfFontError error, string message) : base(message) => Error = error;
}
