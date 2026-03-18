namespace OxidizePdf.NET.Models;

/// <summary>
/// Options for controlling text extraction behavior from parsed PDFs.
/// Defaults match oxidize-pdf core's ExtractionOptions::default().
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
    }
}
