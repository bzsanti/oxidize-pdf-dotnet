namespace OxidizePdf.NET.Models;

/// <summary>
/// Options for controlling text extraction behavior from parsed PDFs.
/// Defaults match oxidize-pdf 2.10.0 <c>ExtractionOptions::default()</c>.
/// When bumping the core dependency, verify these defaults against upstream
/// (<c>oxidize-pdf-core/src/text/extraction.rs</c>, <c>impl Default for
/// ExtractionOptions</c>) — they are duplicated here as plain literals and
/// can drift silently otherwise.
/// </summary>
public class ExtractionOptions
{
    /// <summary>Preserve original layout (spacing/positioning). Default: false.</summary>
    public bool PreserveLayout { get; set; }

    /// <summary>Minimum space width to insert space character (in text space units). Default: 0.3.</summary>
    public double SpaceThreshold { get; set; } = 0.3;

    /// <summary>Minimum vertical distance to insert newline (in page units). Default: 10.0.</summary>
    public double NewlineThreshold { get; set; } = 10.0;

    /// <summary>Sort text fragments by position (useful for multi-column layouts). Default: true.</summary>
    public bool SortByPosition { get; set; } = true;

    /// <summary>Detect and handle columns. Default: false.</summary>
    public bool DetectColumns { get; set; }

    /// <summary>Column separation threshold in page units. Default: 50.0.</summary>
    public double ColumnThreshold { get; set; } = 50.0;

    /// <summary>Merge hyphenated words at line ends. Default: true.</summary>
    public bool MergeHyphenated { get; set; } = true;

    /// <summary>
    /// Threshold for synthesising an implicit space from a <c>TJ</c> numeric
    /// kerning offset, expressed as a fraction of the current font size
    /// (oxidize-pdf 2.10.0, upstream issue #272). When the synthesised advance
    /// exceeds <c>TjSpaceThreshold × font_size</c>, the extractor inserts one
    /// <c>U+0020</c>. Default: 0.2.
    /// </summary>
    public double TjSpaceThreshold { get; set; } = 0.2;

    /// <summary>
    /// Reconstruct visual lines and paragraphs from raw text fragments
    /// (oxidize-pdf 2.10.0, upstream issue #261). When <c>true</c>, the
    /// extractor groups fragments by baseline into line-level fragments,
    /// then groups consecutive lines with normal leading into paragraph
    /// fragments. Default: false (raw per-show-operator fragments).
    /// </summary>
    public bool ReconstructParagraphs { get; set; }

    /// <summary>
    /// Include content inside <c>/Artifact</c> marked-content scopes — page
    /// headers, footers, watermarks, decorative content (oxidize-pdf 2.10.0,
    /// upstream issue #269). Default: false (artifacts filtered, matching
    /// PDF/UA accessibility guidance and RAG use cases).
    /// </summary>
    public bool IncludeArtifacts { get; set; }

    /// <summary>
    /// Validates that all option values are within acceptable ranges.
    /// </summary>
    /// <exception cref="ArgumentException">If any threshold is negative.</exception>
    public void Validate()
    {
        if (SpaceThreshold < 0)
            throw new ArgumentException("SpaceThreshold must be non-negative", nameof(SpaceThreshold));
        if (NewlineThreshold < 0)
            throw new ArgumentException("NewlineThreshold must be non-negative", nameof(NewlineThreshold));
        if (ColumnThreshold < 0)
            throw new ArgumentException("ColumnThreshold must be non-negative", nameof(ColumnThreshold));
        if (TjSpaceThreshold < 0)
            throw new ArgumentException("TjSpaceThreshold must be non-negative", nameof(TjSpaceThreshold));
    }
}
