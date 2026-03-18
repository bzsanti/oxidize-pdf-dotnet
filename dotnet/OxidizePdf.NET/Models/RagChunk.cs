using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// A structure-aware RAG chunk produced by the hybrid chunking pipeline.
/// Contains text, metadata, and context suitable for retrieval-augmented generation.
/// </summary>
public class RagChunk
{
    /// <summary>Sequential chunk index (0-based).</summary>
    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; set; }

    /// <summary>Chunk text content (elements joined by newlines).</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Full text with heading context prepended.</summary>
    [JsonPropertyName("full_text")]
    public string FullText { get; set; } = string.Empty;

    /// <summary>Pages covered by this chunk (1-based, deduplicated, sorted).</summary>
    [JsonPropertyName("page_numbers")]
    public List<int> PageNumbers { get; set; } = new();

    /// <summary>Element type names included in this chunk.</summary>
    [JsonPropertyName("element_types")]
    public List<string> ElementTypes { get; set; } = new();

    /// <summary>Nearest parent heading providing context.</summary>
    [JsonPropertyName("heading_context")]
    public string? HeadingContext { get; set; }

    /// <summary>Approximate token count (word-count proxy).</summary>
    [JsonPropertyName("token_estimate")]
    public int TokenEstimate { get; set; }

    /// <summary>Whether this chunk exceeds the configured max_tokens.</summary>
    [JsonPropertyName("is_oversized")]
    public bool IsOversized { get; set; }
}
