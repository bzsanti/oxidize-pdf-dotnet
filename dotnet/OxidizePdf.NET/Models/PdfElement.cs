using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// A typed document element extracted by the partition pipeline.
/// Represents a semantic unit (title, paragraph, table, etc.) from a PDF.
/// </summary>
public class PdfElement
{
    /// <summary>Element type: "title", "paragraph", "table", "header", "footer", "list_item", "image", "code_block", "key_value".</summary>
    [JsonPropertyName("element_type")]
    public string ElementType { get; set; } = string.Empty;

    /// <summary>Text content of the element.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Page number (1-based).</summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    /// <summary>Bounding box X coordinate.</summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    /// <summary>Bounding box Y coordinate.</summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }

    /// <summary>Bounding box width.</summary>
    [JsonPropertyName("width")]
    public double Width { get; set; }

    /// <summary>Bounding box height.</summary>
    [JsonPropertyName("height")]
    public double Height { get; set; }

    /// <summary>Confidence score (0.0–1.0).</summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
