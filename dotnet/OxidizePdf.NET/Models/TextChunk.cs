using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// A chunk produced by the standalone
/// <see cref="OxidizePdf.NET.Ai.DocumentChunker"/> (RAG-008). Mirrors
/// <c>oxidize_pdf::ai::DocumentChunk</c>'s scalar fields. Distinct from
/// <see cref="DocumentChunk"/>, which is the per-PDF-page chunk record
/// produced by <see cref="OxidizePdf.NET.PdfExtractor"/>.
/// </summary>
public class TextChunk
{
    /// <summary>Unique identifier for this chunk (e.g. <c>"chunk_0"</c>, <c>"chunk_1"</c>).</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The text content of this chunk.</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Estimated number of tokens in this chunk (whitespace word count).</summary>
    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }

    /// <summary>
    /// Page numbers (1-based) where this chunk's content appears. Empty when
    /// the chunker is invoked on raw text (no page metadata available).
    /// </summary>
    [JsonPropertyName("page_numbers")]
    public List<int> PageNumbers { get; set; } = new();

    /// <summary>Index of this chunk in the sequence (0-based).</summary>
    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; set; }
}
