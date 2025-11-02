namespace OxidizePdf.NET.Models;

/// <summary>
/// Options for text chunking optimized for RAG/LLM pipelines
/// </summary>
public class ChunkOptions
{
    /// <summary>
    /// Maximum size of each chunk in characters (default: 512)
    /// </summary>
    /// <remarks>
    /// Adjust based on your embedding model's token limit.
    /// Common values: 256, 512, 1024
    /// </remarks>
    public int MaxChunkSize { get; set; } = 512;

    /// <summary>
    /// Number of characters to overlap between chunks (default: 50)
    /// </summary>
    /// <remarks>
    /// Overlap ensures context continuity between chunks.
    /// Typical range: 10-20% of MaxChunkSize
    /// </remarks>
    public int Overlap { get; set; } = 50;

    /// <summary>
    /// Avoid splitting sentences across chunks (default: true)
    /// </summary>
    /// <remarks>
    /// When enabled, chunks will end at sentence boundaries
    /// to maintain semantic coherence for embeddings.
    /// </remarks>
    public bool PreserveSentenceBoundaries { get; set; } = true;

    /// <summary>
    /// Include metadata (page numbers, bounding boxes) (default: true)
    /// </summary>
    public bool IncludeMetadata { get; set; } = true;

    /// <summary>
    /// Validate options and throw if invalid
    /// </summary>
    internal void Validate()
    {
        if (MaxChunkSize <= 0)
            throw new ArgumentException("MaxChunkSize must be positive", nameof(MaxChunkSize));

        if (Overlap < 0)
            throw new ArgumentException("Overlap must be non-negative", nameof(Overlap));

        if (Overlap >= MaxChunkSize)
            throw new ArgumentException("Overlap must be less than MaxChunkSize", nameof(Overlap));
    }
}
