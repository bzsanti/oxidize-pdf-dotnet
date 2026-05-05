using System.Runtime.InteropServices;
using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Ai;

/// <summary>
/// Standalone text chunker (RAG-008). Mirrors
/// <c>oxidize_pdf::ai::DocumentChunker</c>: operates on raw strings with
/// a fixed-size + overlap strategy in whitespace-separated tokens, plus a
/// best-effort sentence-boundary refinement on the last 10 tokens of each
/// chunk.
/// </summary>
/// <remarks>
/// For PDF-aware chunking with structural awareness, use
/// <see cref="OxidizePdf.NET.PdfExtractor.RagChunksAsync(byte[], System.Threading.CancellationToken)"/>
/// or its overloads.
/// </remarks>
public class DocumentChunker
{
    /// <summary>Target tokens per chunk (whitespace-separated tokens).</summary>
    public int ChunkSize { get; }

    /// <summary>Overlap between consecutive chunks, in tokens. Strictly less than <see cref="ChunkSize"/>.</summary>
    public int Overlap { get; }

    /// <summary>Construct a chunker with the upstream defaults (chunk size 512, overlap 50).</summary>
    public DocumentChunker() : this(512, 50) { }

    /// <summary>
    /// Construct a chunker with explicit size and overlap.
    /// </summary>
    /// <param name="chunkSize">Target tokens per chunk; must be positive.</param>
    /// <param name="overlap">Tokens of overlap; must be non-negative and strictly less than <paramref name="chunkSize"/>.</param>
    /// <exception cref="ArgumentException">If the constraints are violated.</exception>
    public DocumentChunker(int chunkSize, int overlap)
    {
        if (chunkSize <= 0)
            throw new ArgumentException("chunkSize must be positive", nameof(chunkSize));
        if (overlap < 0)
            throw new ArgumentException("overlap must be non-negative", nameof(overlap));
        if (overlap >= chunkSize)
            throw new ArgumentException("overlap must be less than chunkSize", nameof(overlap));

        ChunkSize = chunkSize;
        Overlap = overlap;
    }

    /// <summary>
    /// Chunk a text string into size-bounded overlapping pieces.
    /// </summary>
    /// <param name="text">Input text. Empty input returns an empty list.</param>
    /// <returns>A list of <see cref="TextChunk"/> records with sequential <see cref="TextChunk.ChunkIndex"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="text"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If the FFI call fails (rare for valid input).</exception>
    public List<TextChunk> ChunkText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        IntPtr outJson = IntPtr.Zero;
        try
        {
            var rc = NativeMethods.oxidize_chunk_text(
                text,
                (nuint)ChunkSize,
                (nuint)Overlap,
                out outJson);
            PdfExtractor.ThrowIfError(rc, "oxidize_chunk_text failed");

            var json = Marshal.PtrToStringUTF8(outJson) ?? "[]";
            return JsonSerializer.Deserialize<List<TextChunk>>(json) ?? new List<TextChunk>();
        }
        finally
        {
            if (outJson != IntPtr.Zero)
                NativeMethods.oxidize_free_string(outJson);
        }
    }

    /// <summary>
    /// Estimate the number of tokens in a text string using the upstream
    /// heuristic (RAG-009). Formula: <c>floor(words * 1.33)</c> where
    /// <c>words</c> is the count of whitespace-separated tokens.
    /// </summary>
    /// <param name="text">Input text.</param>
    /// <returns>Estimated token count. <c>0</c> for empty input.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="text"/> is null.</exception>
    public static int EstimateTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var rc = NativeMethods.oxidize_estimate_tokens(text, out var count);
        PdfExtractor.ThrowIfError(rc, "oxidize_estimate_tokens failed");
        return (int)count;
    }
}
