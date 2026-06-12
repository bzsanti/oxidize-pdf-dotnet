using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Ai;

/// <summary>
/// A full-fidelity RAG/LLM document chunk. Mirrors
/// <c>oxidize_pdf::ai::DocumentChunk</c> field-for-field, including the nested
/// <see cref="ChunkMetadata"/>.
/// </summary>
/// <remarks>
/// Distinct from <see cref="OxidizePdf.NET.Models.DocumentChunk"/> (the
/// per-PDF-page chunk record with bounding boxes) and from
/// <see cref="OxidizePdf.NET.Models.TextChunk"/> (a scalar projection without
/// metadata). This is the unit consumed/produced by
/// <see cref="DocumentChunker.ChunkPdf(byte[])"/>,
/// <see cref="DocumentChunker.DocumentLanguage(System.Collections.Generic.IEnumerable{DocumentChunk})"/>,
/// and <see cref="TokenEfficientExporter"/>.
/// </remarks>
public class DocumentChunk
{
    /// <summary>Unique identifier for this chunk (e.g. <c>"chunk_0"</c>).</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The text content of this chunk.</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Estimated number of tokens in this chunk.</summary>
    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }

    /// <summary>Page numbers (1-based) where this chunk's content appears.</summary>
    [JsonPropertyName("page_numbers")]
    public List<int> PageNumbers { get; set; } = new();

    /// <summary>Index of this chunk in the sequence (0-based).</summary>
    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; set; }

    /// <summary>Structural and quality metadata for this chunk.</summary>
    [JsonPropertyName("metadata")]
    public ChunkMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Per-chunk metadata. Mirrors <c>oxidize_pdf::ai::ChunkMetadata</c>.
/// </summary>
public class ChunkMetadata
{
    /// <summary>Where this chunk appears in the full document text.</summary>
    [JsonPropertyName("position")]
    public ChunkPosition Position { get; set; } = new();

    /// <summary>Text-extraction quality confidence in <c>[0.0, 1.0]</c>.</summary>
    [JsonPropertyName("confidence")]
    public float Confidence { get; set; } = 1.0f;

    /// <summary>Whether this chunk's boundary respects sentence boundaries.</summary>
    [JsonPropertyName("sentence_boundary_respected")]
    public bool SentenceBoundaryRespected { get; set; }

    /// <summary>
    /// Detected language for this chunk, if language detection ran; otherwise
    /// <c>null</c>. Not preserved by the token-efficient format round-trip.
    /// </summary>
    [JsonPropertyName("language")]
    public DetectedLanguage? Language { get; set; }
}

/// <summary>
/// Character/page span of a chunk within the document. Mirrors
/// <c>oxidize_pdf::ai::ChunkPosition</c>.
/// </summary>
public class ChunkPosition
{
    /// <summary>Character offset where this chunk starts in the full text.</summary>
    [JsonPropertyName("start_char")]
    public int StartChar { get; set; }

    /// <summary>Character offset where this chunk ends in the full text.</summary>
    [JsonPropertyName("end_char")]
    public int EndChar { get; set; }

    /// <summary>First page (1-based) where this chunk appears.</summary>
    [JsonPropertyName("first_page")]
    public int FirstPage { get; set; }

    /// <summary>Last page (1-based) where this chunk appears.</summary>
    [JsonPropertyName("last_page")]
    public int LastPage { get; set; }
}
