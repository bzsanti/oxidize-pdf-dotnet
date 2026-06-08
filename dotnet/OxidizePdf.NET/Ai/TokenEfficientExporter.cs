using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET.Ai;

/// <summary>
/// Token-efficient, TOON-inspired tabular serializer for RAG chunks (mirrors
/// <c>oxidize_pdf::ai::TokenEfficientExporter</c>, new in 2.13.0).
/// </summary>
/// <remarks>
/// Declares column names once in a header line and emits one tab-separated row
/// per chunk, removing the per-record key overhead that dominates JSON token
/// cost for large chunk sets (~64% fewer tokens on a representative corpus).
/// <see cref="Export"/> and <see cref="Parse"/> are inverses, except that the
/// per-chunk <see cref="ChunkMetadata.Language"/> is not part of the wire format
/// and is therefore <c>null</c> after a round-trip.
/// </remarks>
public static class TokenEfficientExporter
{
    /// <summary>
    /// Serialize chunks to the token-efficient payload string.
    /// </summary>
    /// <param name="chunks">The chunks to serialize. Must not be null.</param>
    /// <returns>The payload string (first line is the format magic <c>#oxct/1</c>).</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="chunks"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If the FFI call fails.</exception>
    public static string Export(IEnumerable<DocumentChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var chunksJson = JsonSerializer.Serialize(chunks as IReadOnlyList<DocumentChunk> ?? chunks.ToList());

        IntPtr outStr = IntPtr.Zero;
        try
        {
            var rc = NativeMethods.oxidize_export_chunks_token_efficient(chunksJson, out outStr);
            PdfExtractor.ThrowIfError(rc, "oxidize_export_chunks_token_efficient failed");
            return Marshal.PtrToStringUTF8(outStr) ?? string.Empty;
        }
        finally
        {
            if (outStr != IntPtr.Zero)
                NativeMethods.oxidize_free_string(outStr);
        }
    }

    /// <summary>
    /// Parse a token-efficient payload back into chunks (inverse of
    /// <see cref="Export"/>).
    /// </summary>
    /// <param name="payload">A payload string previously produced by <see cref="Export"/>.</param>
    /// <returns>The decoded chunks (with <see cref="ChunkMetadata.Language"/> null).</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="payload"/> is null.</exception>
    /// <exception cref="PdfExtractionException">If the payload is malformed.</exception>
    public static List<DocumentChunk> Parse(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        IntPtr outJson = IntPtr.Zero;
        try
        {
            var rc = NativeMethods.oxidize_parse_chunks_token_efficient(payload, out outJson);
            PdfExtractor.ThrowIfError(rc, "oxidize_parse_chunks_token_efficient failed");

            var json = Marshal.PtrToStringUTF8(outJson) ?? "[]";
            return JsonSerializer.Deserialize<List<DocumentChunk>>(json) ?? new List<DocumentChunk>();
        }
        finally
        {
            if (outJson != IntPtr.Zero)
                NativeMethods.oxidize_free_string(outJson);
        }
    }
}
