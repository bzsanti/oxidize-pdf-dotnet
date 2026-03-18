using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Annotation extracted from an existing PDF document.
/// </summary>
public class PdfAnnotation
{
    /// <summary>Annotation subtype (e.g., "Text", "Link", "Highlight").</summary>
    [JsonPropertyName("subtype")]
    public string Subtype { get; set; } = string.Empty;

    /// <summary>Text contents of the annotation (optional).</summary>
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }

    /// <summary>Title/author of the annotation (the /T entry, optional).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>1-based page number where the annotation appears.</summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    /// <summary>Annotation rectangle [x1, y1, x2, y2] in PDF coordinates (optional).</summary>
    [JsonPropertyName("rect")]
    public double[]? Rect { get; set; }
}
