using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// A semantic chunk produced by the element-boundary-aware
/// <see cref="OxidizePdf.NET.Pipeline.SemanticChunkConfig"/> chunker.
/// Differs from <see cref="RagChunk"/> in that it preserves structural
/// unity: titles, tables, and code blocks are kept whole when
/// <see cref="OxidizePdf.NET.Pipeline.SemanticChunkConfig.RespectElementBoundaries"/>
/// is true. Mirrors the FFI <c>SemanticChunkResult</c> wire format.
/// </summary>
public class SemanticChunk
{
    /// <summary>Index of this chunk in the sequence (0-based).</summary>
    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; set; }

    /// <summary>Concatenated text of all elements in this chunk (separated by <c>\n</c>).</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Page numbers (1-based) where this chunk's elements live.</summary>
    [JsonPropertyName("page_numbers")]
    public List<int> PageNumbers { get; set; } = new();

    /// <summary>Estimated number of tokens in this chunk (whitespace word count).</summary>
    [JsonPropertyName("token_estimate")]
    public int TokenEstimate { get; set; }

    /// <summary>True if this chunk exceeds the configured max-tokens budget but couldn't be split further.</summary>
    [JsonPropertyName("is_oversized")]
    public bool IsOversized { get; set; }
}
