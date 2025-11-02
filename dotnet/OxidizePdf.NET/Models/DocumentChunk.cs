using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Represents a text chunk extracted from a PDF, optimized for RAG/LLM pipelines
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Chunk index in the document (0-based)
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// Source page number (1-based)
    /// </summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    /// <summary>
    /// Extracted text content
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Extraction confidence score (0.0 - 1.0)
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Bounding box of the chunk on the page
    /// </summary>
    public BoundingBox BoundingBox { get; set; } = new();

    /// <summary>
    /// X coordinate on page
    /// </summary>
    [JsonPropertyName("x")]
    public double X
    {
        get => BoundingBox.X;
        set => BoundingBox.X = value;
    }

    /// <summary>
    /// Y coordinate on page
    /// </summary>
    [JsonPropertyName("y")]
    public double Y
    {
        get => BoundingBox.Y;
        set => BoundingBox.Y = value;
    }

    /// <summary>
    /// Width of bounding box
    /// </summary>
    [JsonPropertyName("width")]
    public double Width
    {
        get => BoundingBox.Width;
        set => BoundingBox.Width = value;
    }

    /// <summary>
    /// Height of bounding box
    /// </summary>
    [JsonPropertyName("height")]
    public double Height
    {
        get => BoundingBox.Height;
        set => BoundingBox.Height = value;
    }
}

/// <summary>
/// Bounding box coordinates for spatial positioning
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// X coordinate (left edge)
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate (bottom edge in PDF coordinates)
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Width of the box
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Height of the box
    /// </summary>
    public double Height { get; set; }
}
