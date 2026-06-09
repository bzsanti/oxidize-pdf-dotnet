using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// A single page-label range: the numbering style (and optional prefix /
/// starting value) applied from <see cref="StartPage"/> until the next range.
/// Mirrors a <c>oxidize_pdf::PageLabel</c> bound to a start page.
/// </summary>
public sealed class PdfPageLabelRange
{
    /// <summary>0-based index of the first page this range applies to.</summary>
    [JsonPropertyName("start_page")]
    public int StartPage { get; init; }

    /// <summary>Numbering style for this range.</summary>
    [JsonPropertyName("style")]
    public PdfPageLabelStyle Style { get; init; }

    /// <summary>Optional label prefix (e.g. <c>"A-"</c> → <c>"A-1", "A-2"</c>).</summary>
    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    /// <summary>Optional starting value for the numeric portion (default 1).</summary>
    [JsonPropertyName("start_at")]
    public uint? StartAt { get; init; }
}

/// <summary>
/// Ordered set of <see cref="PdfPageLabelRange"/> ranges defining a document's
/// page-label tree (custom page numbering). Mirrors
/// <c>oxidize_pdf::PageLabelTree</c>.
/// </summary>
public sealed class PdfPageLabels
{
    /// <summary>The ranges, in document order. At least one is required to apply.</summary>
    [JsonPropertyName("ranges")]
    public List<PdfPageLabelRange> Ranges { get; init; } = new();

    /// <summary>Start an empty page-label set to build with <see cref="AddRange"/>.</summary>
    public static PdfPageLabels Create() => new();

    /// <summary>
    /// Append a numbering range. Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="startPage">0-based first page of the range.</param>
    /// <param name="style">Numbering style.</param>
    /// <param name="prefix">Optional label prefix.</param>
    /// <param name="startAt">Optional starting value for the numeric portion.</param>
    public PdfPageLabels AddRange(
        int startPage,
        PdfPageLabelStyle style,
        string? prefix = null,
        uint? startAt = null)
    {
        Ranges.Add(new PdfPageLabelRange
        {
            StartPage = startPage,
            Style = style,
            Prefix = prefix,
            StartAt = startAt,
        });
        return this;
    }
}
