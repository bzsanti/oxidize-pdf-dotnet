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
    /// Core ignores zero/out-of-bounds points and preserves supplied order;
    /// points are not sorted or deduplicated.
    /// </summary>
    public int[]? SplitAt { get; init; }

    /// <summary>Copy Title, Author, Subject and Keywords (default true).
    /// Does not preserve XMP, custom Info fields or document structure.</summary>
    public bool PreserveMetadata { get; init; } = true;

    /// <summary>Requested optimization (default false). Core 5.4.2 ignores this
    /// flag; enabling it does not optimize or promise smaller output.</summary>
    public bool Optimize { get; init; }

    /// <summary>Suggested filename pattern, default page_{}.pdf. Never a write path.
    /// Supports {n} (one-based output ordinal), {} (one-based page or start-end),
    /// {page} for SinglePages, and {start}/{end} for other modes. Names must be
    /// portable filenames (at most 200 UTF-8 bytes). Named results must be unique ignoring case. Unsupported tokens are rejected.</summary>
    public string OutputPattern { get; init; } = "page_{}.pdf";

    internal string ToJson(bool named = false)
    {
        if (!Enum.IsDefined(Mode)) throw new ArgumentException("Invalid split mode", nameof(Mode));
        if (Mode == PdfSplitMode.ChunkSize && ChunkSize < 1)
            throw new ArgumentOutOfRangeException(nameof(ChunkSize));
        if (Mode == PdfSplitMode.Ranges && (Ranges is null || Ranges.Length == 0 || Ranges.Any(r => r.From < 0 || r.To < r.From)))
            throw new ArgumentException("Nonempty ascending nonnegative ranges are required", nameof(Ranges));
        if (Mode == PdfSplitMode.SplitAt && (SplitAt is null || SplitAt.Any(p => p < 0)))
            throw new ArgumentException("Nonnegative split points are required", nameof(SplitAt));
        ArgumentNullException.ThrowIfNull(OutputPattern);
        return JsonSerializer.Serialize(new
        {
            mode = Mode.ToString(),
            chunk_size = ChunkSize,
            ranges = Ranges?.Select(r => new[] { r.From, r.To }).ToArray(),
            split_at = SplitAt?.ToArray(),
            preserve_metadata = PreserveMetadata,
            optimize = Optimize,
            output_pattern = OutputPattern,
            named,
        });
    }
}
