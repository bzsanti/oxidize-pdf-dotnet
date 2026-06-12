namespace OxidizePdf.NET;

/// <summary>
/// Writer configuration for <see cref="PdfDocument.SaveToBytes(PdfSaveOptions)"/>.
/// Controls PDF version, cross-reference layout, object packing, and stream
/// compression.
/// </summary>
public sealed class PdfSaveOptions
{
    /// <summary>
    /// Use XRef streams (PDF 1.5+) instead of a traditional XRef table.
    /// Smaller file size at the cost of PDF 1.4 compatibility.
    /// </summary>
    public bool UseXrefStreams { get; init; }

    /// <summary>
    /// Pack multiple indirect objects into object streams (PDF 1.5+).
    /// Requires <see cref="UseXrefStreams"/> to be effective.
    /// </summary>
    public bool UseObjectStreams { get; init; }

    /// <summary>
    /// PDF version header (e.g. "1.4", "1.5", "1.7"). Default is "1.7".
    /// </summary>
    public string PdfVersion { get; init; } = "1.7";

    /// <summary>
    /// Apply FlateDecode compression to content streams. Default is <c>true</c>.
    /// </summary>
    public bool CompressStreams { get; init; } = true;

    /// <summary>PDF 1.7 with traditional XRef table and compressed content streams. Default.</summary>
    public static PdfSaveOptions Default() => new();

    /// <summary>PDF 1.5 with XRef streams, object streams, and compression — smallest output.</summary>
    public static PdfSaveOptions Modern() => new()
    {
        UseXrefStreams = true,
        UseObjectStreams = true,
        PdfVersion = "1.5",
        CompressStreams = true,
    };

    /// <summary>PDF 1.4 legacy compatibility — no xref/object streams.</summary>
    public static PdfSaveOptions Legacy() => new()
    {
        UseXrefStreams = false,
        UseObjectStreams = false,
        PdfVersion = "1.4",
        CompressStreams = true,
    };
}
