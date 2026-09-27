using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>One semantic target for text removal or visual masking.</summary>
public sealed class PdfRedactionEntity
{
    /// <summary>Caller-assigned entity identifier returned in the report.</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    /// <summary>Upstream camelCase entity type (for example personName), or a custom type name.</summary>
    [JsonPropertyName("type")] public string Type { get; set; } = "text";
    /// <summary>Exact target text. Irreversible removal requires a complete ASCII text operand match.</summary>
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    /// <summary>Target region and one-based page number.</summary>
    [JsonPropertyName("bounds")] public PdfRedactionBounds Bounds { get; set; } = new();
}

/// <summary>Region in PDF coordinates, using a one-based page number.</summary>
public sealed class PdfRedactionBounds
{
    /// <summary>Left edge.</summary>
    [JsonPropertyName("x")] public float X { get; set; }
    /// <summary>Bottom edge.</summary>
    [JsonPropertyName("y")] public float Y { get; set; }
    /// <summary>Positive width.</summary>
    [JsonPropertyName("width")] public float Width { get; set; }
    /// <summary>Positive height.</summary>
    [JsonPropertyName("height")] public float Height { get; set; }
    /// <summary>One-based page number.</summary>
    [JsonPropertyName("page")] public uint Page { get; set; } = 1;
}

/// <summary>Output and explicit security semantics of the operation.</summary>
public sealed class PdfRedactionResult
{
    /// <summary>Rebuilt PDF output; prior structure may be discarded.</summary>
    [JsonPropertyName("pdf_bytes")] public byte[] PdfBytes { get; set; } = [];
    /// <summary>Applied targets, mode and residual risks.</summary>
    [JsonPropertyName("report")] public PdfRedactionReport Report { get; set; } = new();
}

/// <summary>Upstream report distinguishing removal from recoverable visual masking.</summary>
public sealed class PdfRedactionReport
{
    /// <summary>VisualMask or Irreversible.</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "";
    /// <summary>True only if upstream completed irreversible removal and its checks passed.</summary>
    [JsonPropertyName("is_irreversible")] public bool IsIrreversible { get; set; }
    /// <summary>Known ways target data may remain recoverable.</summary>
    [JsonPropertyName("residual_risks")] public string[] ResidualRisks { get; set; } = [];
    /// <summary>Number of processed targets.</summary>
    [JsonPropertyName("redacted_count")] public uint RedactedCount { get; set; }
    /// <summary>Affected one-based page numbers.</summary>
    [JsonPropertyName("pages_affected")] public uint[] PagesAffected { get; set; } = [];
    /// <summary>Processed targets.</summary>
    [JsonPropertyName("entries")] public PdfRedactionEntry[] Entries { get; set; } = [];
}

/// <summary>One processed semantic target.</summary>
public sealed class PdfRedactionEntry
{
    /// <summary>Caller target identifier.</summary>
    [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";
    /// <summary>Target type.</summary>
    [JsonPropertyName("entity_type")] public string EntityType { get; set; } = "";
    /// <summary>One-based page number.</summary>
    [JsonPropertyName("page")] public uint Page { get; set; }
}

/// <summary>Explicit irreversible text removal and recoverable visual masking.</summary>
public static class PdfRedaction
{
    /// <summary>
    /// Removes exact complete ASCII Tj/TJ targets and rebuilds the PDF, dropping prior
    /// revisions and auxiliary document data. Unsupported or ambiguous input throws
    /// without returning output. There is no fallback to visual masking.
    /// </summary>
    public static Task<PdfRedactionResult> RemoveIrreversiblyAsync(byte[] pdf,
        IEnumerable<PdfRedactionEntity> entities, IEnumerable<string> entityTypes, CancellationToken ct = default)
        => CallAsync(pdf, entities, entityTypes, true, null, ct);
    /// <summary>
    /// Draws opaque rectangles, optionally with placeholder text. Underlying content
    /// remains recoverable. Always inspect the risk report; this is not secure removal.
    /// </summary>
    public static Task<PdfRedactionResult> MaskVisuallyAsync(byte[] pdf,
        IEnumerable<PdfRedactionEntity> entities, IEnumerable<string> entityTypes,
        string? placeholder = null, CancellationToken ct = default)
        => CallAsync(pdf, entities, entityTypes, false, placeholder, ct);
    private static Task<PdfRedactionResult> CallAsync(byte[] pdf, IEnumerable<PdfRedactionEntity> entities,
        IEnumerable<string> types, bool irreversible, string? placeholder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(types);
        if (pdf.Length == 0) throw new ArgumentException("PDF bytes cannot be empty", nameof(pdf));
        ct.ThrowIfCancellationRequested();
        var request = JsonSerializer.Serialize(new { irreversible, entities = entities.ToArray(), entity_types = types.ToArray(), placeholder });
        return Task.Run(() => Call(pdf, request), ct);
    }
    private static PdfRedactionResult Call(byte[] pdf, string request)
    {
        var pinned = GCHandle.Alloc(pdf, GCHandleType.Pinned);
        IntPtr output = IntPtr.Zero;
        try
        {
            var status = RedactionNative(pinned.AddrOfPinnedObject(), (nuint)pdf.Length, request, out output);
            if (status != 0) throw new PdfExtractionException(NativeMethods.GetLastError() ?? "Redaction operation failed");
            return JsonSerializer.Deserialize<PdfRedactionResult>(Marshal.PtrToStringUTF8(output)
                ?? throw new PdfExtractionException("Missing redaction response"))
                ?? throw new PdfExtractionException("Invalid redaction response");
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_string(output);
            pinned.Free();
        }
    }
    [DllImport("oxidize_pdf_ffi", EntryPoint = "oxidize_redaction", CallingConvention = CallingConvention.Cdecl)]
    private static extern int RedactionNative(IntPtr pdf, nuint length,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string request, out IntPtr output);
}
