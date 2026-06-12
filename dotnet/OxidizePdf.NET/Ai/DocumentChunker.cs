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

    /// <summary>
    /// Whether per-chunk language detection runs when chunking a PDF via
    /// <see cref="ChunkPdf(byte[])"/>. Disabled by default; toggle with
    /// <see cref="WithLanguageDetection(bool)"/>.
    /// </summary>
    public bool LanguageDetectionEnabled { get; private init; }

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

    /// <summary>
    /// Return a chunker with the same size/overlap and per-chunk language
    /// detection enabled or disabled (mirrors
    /// <c>DocumentChunker::with_language_detection</c>, 2.13.0).
    /// </summary>
    /// <param name="enabled">Whether to detect each chunk's language.</param>
    /// <returns>A new <see cref="DocumentChunker"/> with the flag applied.</returns>
    public DocumentChunker WithLanguageDetection(bool enabled) =>
        new(ChunkSize, Overlap) { LanguageDetectionEnabled = enabled };

    /// <summary>
    /// Chunk a PDF into full-fidelity <see cref="DocumentChunk"/> records,
    /// tracking page numbers. When <see cref="LanguageDetectionEnabled"/> is
    /// set, each chunk's <see cref="ChunkMetadata.Language"/> is populated.
    /// </summary>
    /// <param name="pdfBytes">The PDF document bytes. Must not be null.</param>
    /// <returns>The chunks in sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If parsing or chunking fails.</exception>
    public List<DocumentChunk> ChunkPdf(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr outJson = IntPtr.Zero;
        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var rc = NativeMethods.oxidize_chunk_pdf(
                pdfPtr,
                (nuint)pdfBytes.Length,
                (nuint)ChunkSize,
                (nuint)Overlap,
                (byte)(LanguageDetectionEnabled ? 1 : 0),
                out outJson);
            PdfExtractor.ThrowIfError(rc, "oxidize_chunk_pdf failed");

            var json = Marshal.PtrToStringUTF8(outJson) ?? "[]";
            return JsonSerializer.Deserialize<List<DocumentChunk>>(json) ?? new List<DocumentChunk>();
        }
        finally
        {
            if (outJson != IntPtr.Zero)
                NativeMethods.oxidize_free_string(outJson);
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);
        }
    }

    /// <summary>
    /// Compute the dominant language across the given chunks, weighted by chunk
    /// content length (mirrors <c>DocumentChunker::document_language</c>).
    /// </summary>
    /// <param name="chunks">Chunks that may carry per-chunk languages.</param>
    /// <returns>The dominant language, or <c>null</c> if no chunk has one.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="chunks"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If the FFI call fails.</exception>
    public static DetectedLanguage? DocumentLanguage(IEnumerable<DocumentChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var chunksJson = JsonSerializer.Serialize(chunks as IReadOnlyList<DocumentChunk> ?? chunks.ToList());

        IntPtr outJson = IntPtr.Zero;
        try
        {
            var rc = NativeMethods.oxidize_document_language(chunksJson, out outJson);
            PdfExtractor.ThrowIfError(rc, "oxidize_document_language failed");

            var json = Marshal.PtrToStringUTF8(outJson) ?? "null";
            return JsonSerializer.Deserialize<DetectedLanguage?>(json);
        }
        finally
        {
            if (outJson != IntPtr.Zero)
                NativeMethods.oxidize_free_string(outJson);
        }
    }
}
