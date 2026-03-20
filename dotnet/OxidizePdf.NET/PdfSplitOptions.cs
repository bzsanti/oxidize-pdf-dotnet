using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>
/// Specifies how a PDF should be split.
/// </summary>
public enum PdfSplitMode
{
    /// <summary>Each page becomes its own PDF.</summary>
    SinglePages,
    /// <summary>Pages are grouped into chunks of a fixed size.</summary>
    ChunkSize,
    /// <summary>Pages are grouped according to explicit 0-based ranges.</summary>
    Ranges,
    /// <summary>Document is split at specific 0-based page indices.</summary>
    SplitAt,
}

/// <summary>
/// Options that control how <see cref="PdfOperations.SplitAsync(byte[], PdfSplitOptions, CancellationToken)"/>
/// splits a PDF document.
/// </summary>
public sealed class PdfSplitOptions
{
    /// <summary>Gets the split mode. Default is <see cref="PdfSplitMode.SinglePages"/>.</summary>
    public PdfSplitMode Mode { get; init; } = PdfSplitMode.SinglePages;

    /// <summary>
    /// Number of pages per chunk. Only used when <see cref="Mode"/> is
    /// <see cref="PdfSplitMode.ChunkSize"/>. Must be at least 1.
    /// </summary>
    public int ChunkSize { get; init; } = 1;

    /// <summary>
    /// Explicit page ranges (0-based, inclusive on both ends). Only used when
    /// <see cref="Mode"/> is <see cref="PdfSplitMode.Ranges"/>.
    /// </summary>
    public (int From, int To)[]? Ranges { get; init; }

    /// <summary>
    /// 0-based page indices at which to split. Each index starts a new chunk.
    /// Only used when <see cref="Mode"/> is <see cref="PdfSplitMode.SplitAt"/>.
    /// </summary>
    public int[]? SplitAt { get; init; }

    /// <summary>Serialises the options to the JSON format expected by the native FFI.</summary>
    internal string ToJson() => Mode switch
    {
        PdfSplitMode.SinglePages => """{"mode":"SinglePages"}""",
        PdfSplitMode.ChunkSize => $$$"""{"mode":"ChunkSize","chunk_size":{{{ChunkSize}}}}""",
        PdfSplitMode.Ranges => JsonSerializer.Serialize(new
        {
            mode = "Ranges",
            ranges = Ranges!.Select(r => new[] { r.From, r.To }).ToArray(),
        }),
        PdfSplitMode.SplitAt => JsonSerializer.Serialize(new
        {
            mode = "SplitAt",
            split_at = SplitAt,
        }),
        _ => throw new ArgumentException($"Invalid split mode: {Mode}"),
    };
}
