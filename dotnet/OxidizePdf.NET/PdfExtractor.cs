using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET;

/// <summary>
/// High-level API for PDF text extraction with RAG/LLM optimization.
/// This class is stateless and does not hold native resources between method calls.
/// Each extraction operation allocates and releases resources within the call.
/// </summary>
public class PdfExtractor
{
    /// <summary>
    /// Default maximum file size (100 MB)
    /// </summary>
    public const long DefaultMaxFileSizeBytes = 100 * 1024 * 1024;

    private readonly long _maxFileSizeBytes;

    /// <summary>
    /// Creates a new PdfExtractor with the default maximum file size of 100 MB.
    /// </summary>
    public PdfExtractor() : this(DefaultMaxFileSizeBytes)
    {
    }

    /// <summary>
    /// Creates a new PdfExtractor with a custom maximum file size.
    /// </summary>
    /// <param name="maxFileSizeBytes">Maximum allowed PDF file size in bytes. Must be positive.</param>
    /// <exception cref="ArgumentException">If maxFileSizeBytes is zero or negative.</exception>
    public PdfExtractor(long maxFileSizeBytes)
    {
        if (maxFileSizeBytes <= 0)
            throw new ArgumentException("maxFileSizeBytes must be a positive value", nameof(maxFileSizeBytes));

        _maxFileSizeBytes = maxFileSizeBytes;
    }

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
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled</exception>
    /// <exception cref="PdfExtractionException">If extraction fails</exception>
    public Task<string> ExtractTextAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        // Check cancellation early before any work
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        // Check cancellation again before expensive FFI call
        cancellationToken.ThrowIfCancellationRequested();

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
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled</exception>
    /// <exception cref="PdfExtractionException">If extraction fails</exception>
    public Task<List<DocumentChunk>> ExtractChunksAsync(
        byte[] pdfBytes,
        ChunkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Check cancellation early before any work
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        options ??= new ChunkOptions();
        options.Validate();

        // Check cancellation again before expensive FFI call
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ExtractChunks(pdfBytes, options), cancellationToken);
    }

    /// <summary>
    /// Get the number of pages in a PDF
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of pages in the PDF</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled</exception>
    /// <exception cref="PdfExtractionException">If extraction fails</exception>
    public Task<int> GetPageCountAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetPageCount(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Extract plain text from a specific page of a PDF
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted plain text from the specified page</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber is less than 1 or exceeds page count</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled</exception>
    /// <exception cref="PdfExtractionException">If extraction fails</exception>
    public Task<string> ExtractTextFromPageAsync(byte[] pdfBytes, int pageNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1 (1-based indexing)");
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ExtractTextFromPage(pdfBytes, pageNumber), cancellationToken);
    }

    /// <summary>
    /// Extract text chunks from a specific page of a PDF
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="options">Chunking options (null for defaults)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of text chunks with metadata from the specified page</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber is less than 1 or exceeds page count</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled</exception>
    /// <exception cref="PdfExtractionException">If extraction fails</exception>
    public Task<List<DocumentChunk>> ExtractChunksFromPageAsync(
        byte[] pdfBytes,
        int pageNumber,
        ChunkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1 (1-based indexing)");
        ValidatePdfSize(pdfBytes);

        options ??= new ChunkOptions();
        options.Validate();

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ExtractChunksFromPage(pdfBytes, pageNumber, options), cancellationToken);
    }

    private void ValidatePdfSize(byte[] pdfBytes)
    {
        if (pdfBytes.LongLength > _maxFileSizeBytes)
        {
            throw new ArgumentException(
                $"PDF size ({pdfBytes.LongLength:N0} bytes) exceeds maximum allowed size ({_maxFileSizeBytes:N0} bytes). " +
                "Consider using a smaller file or increasing the maxFileSizeBytes limit.",
                nameof(pdfBytes));
        }
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

    private int GetPageCount(byte[] pdfBytes)
    {
        IntPtr pdfPtr = IntPtr.Zero;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_get_page_count(
                pdfPtr,
                (nuint)pdfBytes.Length,
                out var pageCount
            );

            ThrowIfError(result, "Failed to get page count from PDF");

            return (int)pageCount;
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);
        }
    }

    private string ExtractTextFromPage(byte[] pdfBytes, int pageNumber)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr textPtr = IntPtr.Zero;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var result = NativeMethods.oxidize_extract_text_from_page(
                pdfPtr,
                (nuint)pdfBytes.Length,
                (nuint)pageNumber,
                out textPtr
            );

            ThrowIfError(result, $"Failed to extract text from page {pageNumber}");

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

    private List<DocumentChunk> ExtractChunksFromPage(byte[] pdfBytes, int pageNumber, ChunkOptions options)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr jsonPtr = IntPtr.Zero;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var nativeOptions = new NativeMethods.ChunkOptionsNative
            {
                MaxChunkSize = (nuint)options.MaxChunkSize,
                Overlap = (nuint)options.Overlap,
                PreserveSentenceBoundaries = options.PreserveSentenceBoundaries,
                IncludeMetadata = options.IncludeMetadata
            };

            var result = NativeMethods.oxidize_extract_chunks_from_page(
                pdfPtr,
                (nuint)pdfBytes.Length,
                (nuint)pageNumber,
                ref nativeOptions,
                out jsonPtr
            );

            ThrowIfError(result, $"Failed to extract chunks from page {pageNumber}");

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

        // Get detailed error message from Rust
        var rustError = NativeMethods.GetLastError();
        var detailedMessage = !string.IsNullOrEmpty(rustError)
            ? $"{message}: {rustError}"
            : $"{message}: {error}";

        throw new PdfExtractionException(detailedMessage);
    }
}

/// <summary>
/// Exception thrown when PDF extraction fails
/// </summary>
public class PdfExtractionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the PdfExtractionException class with a specified error message
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    public PdfExtractionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the PdfExtractionException class with a specified error message and a reference to the inner exception
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    public PdfExtractionException(string message, Exception innerException)
        : base(message, innerException) { }
}
