using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET;

/// <summary>
/// High-level API for PDF text extraction with RAG/LLM optimization
/// </summary>
public class PdfExtractor : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Get the version of the native library
    /// </summary>
    public static string Version
    {
        get
        {
            IntPtr versionPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.oxidize_version(out versionPtr);
                ThrowIfError(result, "Failed to get version");

                return Marshal.PtrToStringUTF8(versionPtr) ?? "Unknown";
            }
            finally
            {
                if (versionPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(versionPtr);
            }
        }
    }

    /// <summary>
    /// Extract plain text from PDF bytes
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array</param>
    /// <param name="cancellationToken">Cancellation token (currently not supported by native layer)</param>
    /// <returns>Extracted plain text</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null</exception>
    /// <exception cref="PdfExtractionException">If extraction fails</exception>
    public Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ThrowIfDisposed();

        return Task.Run(() => ExtractText(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Extract text chunks optimized for RAG/LLM pipelines
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array</param>
    /// <param name="options">Chunking options (null for defaults)</param>
    /// <param name="cancellationToken">Cancellation token (currently not supported by native layer)</param>
    /// <returns>List of text chunks with metadata</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null</exception>
    /// <exception cref="PdfExtractionException">If extraction fails</exception>
    public Task<List<DocumentChunk>> ExtractChunksAsync(
        byte[] pdfBytes,
        ChunkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ThrowIfDisposed();

        options ??= new ChunkOptions();
        options.Validate();

        return Task.Run(() => ExtractChunks(pdfBytes, options), cancellationToken);
    }

    private string ExtractText(byte[] pdfBytes)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr textPtr = IntPtr.Zero;

        try
        {
            // Pin PDF bytes in memory
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            // Call native function
            var result = NativeMethods.oxidize_extract_text(
                pdfPtr,
                (nuint)pdfBytes.Length,
                out textPtr
            );

            ThrowIfError(result, "Failed to extract text from PDF");

            // Marshal result back to C#
            return Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);

            if (textPtr != IntPtr.Zero)
                NativeMethods.oxidize_free_string(textPtr);
        }
    }

    private List<DocumentChunk> ExtractChunks(byte[] pdfBytes, ChunkOptions options)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr jsonPtr = IntPtr.Zero;

        try
        {
            // Pin PDF bytes in memory
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            // Convert options to native struct
            var nativeOptions = new NativeMethods.ChunkOptionsNative
            {
                MaxChunkSize = (nuint)options.MaxChunkSize,
                Overlap = (nuint)options.Overlap,
                PreserveSentenceBoundaries = options.PreserveSentenceBoundaries,
                IncludeMetadata = options.IncludeMetadata
            };

            // Call native function
            var result = NativeMethods.oxidize_extract_chunks(
                pdfPtr,
                (nuint)pdfBytes.Length,
                ref nativeOptions,
                out jsonPtr
            );

            ThrowIfError(result, "Failed to extract chunks from PDF");

            // Deserialize JSON result
            var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
            return JsonSerializer.Deserialize<List<DocumentChunk>>(json)
                ?? new List<DocumentChunk>();
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);

            if (jsonPtr != IntPtr.Zero)
                NativeMethods.oxidize_free_string(jsonPtr);
        }
    }

    private static void ThrowIfError(int errorCode, string message)
    {
        if (errorCode == (int)NativeMethods.ErrorCode.Success)
            return;

        var error = (NativeMethods.ErrorCode)errorCode;
        throw new PdfExtractionException($"{message}: {error}");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PdfExtractor));
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Exception thrown when PDF extraction fails
/// </summary>
public class PdfExtractionException : Exception
{
    public PdfExtractionException(string message) : base(message) { }
    public PdfExtractionException(string message, Exception innerException)
        : base(message, innerException) { }
}
