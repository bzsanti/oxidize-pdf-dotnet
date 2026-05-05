using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OxidizePdf.NET.Ai;
using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;

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
    /// Partition a PDF into typed semantic elements (title, paragraph, table, etc.).
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of semantic elements with type, text, page number, and bounding box.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If partitioning fails.</exception>
    public Task<List<PdfElement>> PartitionAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Partition(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Partition a PDF using a pre-configured <see cref="ExtractionProfile"/>.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="profile">Extraction profile selecting partitioner defaults
    /// tuned for a class of documents (general, academic, form, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of semantic elements for the chosen profile.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty or exceeds the configured maximum size.</exception>
    /// <exception cref="PdfExtractionException">If partitioning fails or the profile discriminant is rejected by the FFI.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    public Task<List<PdfElement>> PartitionAsync(
        byte[] pdfBytes,
        ExtractionProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => PartitionWithProfile(pdfBytes, profile), cancellationToken);
    }

    /// <summary>
    /// Partition a PDF into typed semantic elements using an explicit
    /// <see cref="PartitionConfig"/>. Use this when the defaults from a
    /// profile aren't enough — custom title font ratio, header/footer
    /// zones, table confidence threshold, or a non-default
    /// <see cref="ReadingOrderStrategy"/>.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="config">Partition configuration. Validated client-side
    /// via <see cref="PartitionConfig.Validate"/> before any FFI call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of semantic elements.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> or <paramref name="config"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty, exceeds the configured maximum size, or <paramref name="config"/> fails validation.</exception>
    /// <exception cref="PdfExtractionException">If partitioning fails inside the FFI.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    public Task<List<PdfElement>> PartitionAsync(
        byte[] pdfBytes,
        PartitionConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(config);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        config.Validate();

        cancellationToken.ThrowIfCancellationRequested();

        var json = config.ToJson();
        return Task.Run(() => PartitionWithConfig(pdfBytes, json), cancellationToken);
    }

    /// <summary>
    /// Extract structure-aware RAG chunks from a PDF using the hybrid chunking pipeline.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of RAG-ready chunks with text, context, page numbers, and token estimates.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If chunking fails.</exception>
    public Task<List<RagChunk>> RagChunksAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ExtractRagChunks(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Extract structure-aware RAG chunks using a pre-configured
    /// <see cref="ExtractionProfile"/>. Combines the profile's partitioner
    /// defaults with the upstream <c>HybridChunker::default()</c> chunking.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="profile">Extraction profile selecting partitioner defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of RAG-ready chunks for the chosen profile.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty or exceeds the configured maximum size.</exception>
    /// <exception cref="PdfExtractionException">If chunking fails or the profile discriminant is rejected by the FFI.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    public Task<List<RagChunk>> RagChunksAsync(
        byte[] pdfBytes,
        ExtractionProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => RagChunksWithProfile(pdfBytes, profile), cancellationToken);
    }

    /// <summary>
    /// Extract structure-aware RAG chunks with optional partition and hybrid
    /// chunk configs. Pass <c>null</c> for either to use the corresponding
    /// upstream default (lets callers tune just the chunk size, just the
    /// partitioner, both, or neither).
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="partitionConfig">Optional partition configuration. <c>null</c> uses <c>PartitionConfig::default()</c>.</param>
    /// <param name="hybridConfig">Optional hybrid-chunker configuration. <c>null</c> uses <c>HybridChunkConfig::default()</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of RAG-ready chunks.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty/oversize, or either non-null config fails validation.</exception>
    /// <exception cref="PdfExtractionException">If chunking fails inside the FFI.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    public Task<List<RagChunk>> RagChunksAsync(
        byte[] pdfBytes,
        PartitionConfig? partitionConfig,
        HybridChunkConfig? hybridConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        partitionConfig?.Validate();
        hybridConfig?.Validate();

        cancellationToken.ThrowIfCancellationRequested();

        var partitionJson = partitionConfig?.ToJson();
        var hybridJson = hybridConfig?.ToJson();
        return Task.Run(() => RagChunksWithConfigs(pdfBytes, partitionJson, hybridJson), cancellationToken);
    }

    /// <summary>
    /// Extract semantic (element-boundary-aware) chunks from a PDF. The
    /// <see cref="SemanticChunkConfig"/> chunker preserves structural unity:
    /// titles, tables, and code blocks are kept whole when
    /// <see cref="SemanticChunkConfig.RespectElementBoundaries"/> is true.
    /// Use this when downstream consumers care about structure over merging
    /// adjacent prose.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="config">Semantic-chunker configuration. <c>null</c> uses
    /// <c>SemanticChunkConfig::default()</c> (max_tokens=512, overlap=50,
    /// respect_element_boundaries=true).</param>
    /// <param name="partitionConfig">Optional partition configuration for the
    /// pre-chunking element extraction. <c>null</c> uses
    /// <c>PartitionConfig::default()</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of semantic chunks.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty/oversize, or either non-null config fails validation.</exception>
    /// <exception cref="PdfExtractionException">If chunking fails inside the FFI.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    public Task<List<SemanticChunk>> SemanticChunksAsync(
        byte[] pdfBytes,
        SemanticChunkConfig? config = null,
        PartitionConfig? partitionConfig = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        config ??= new SemanticChunkConfig();
        config.Validate();
        partitionConfig?.Validate();

        cancellationToken.ThrowIfCancellationRequested();

        var partitionJson = partitionConfig?.ToJson();
        var semanticJson = config.ToJson();
        return Task.Run(
            () => SemanticChunksImpl(pdfBytes, partitionJson, semanticJson),
            cancellationToken);
    }

    /// <summary>
    /// Export PDF content as Markdown.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Markdown representation of the PDF content.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If export fails.</exception>
    public Task<string> ToMarkdownAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => StructuredExport(pdfBytes, NativeMethods.oxidize_to_markdown, "markdown"), cancellationToken);
    }

    /// <summary>
    /// Export PDF content as Markdown using explicit
    /// <see cref="MarkdownOptions"/> (RAG-012). The four flag combinations
    /// produce four distinct outputs (FFI dispatches to the matching upstream
    /// static exporter):
    /// <list type="bullet">
    ///   <item><c>(true, true)</c>: YAML frontmatter + per-page markers.</item>
    ///   <item><c>(true, false)</c>: YAML frontmatter, no page markers.</item>
    ///   <item><c>(false, true)</c>: page markers, no YAML.</item>
    ///   <item><c>(false, false)</c>: plain text under a basic <c># Document</c> heading.</item>
    /// </list>
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="options">Markdown export options (required).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Markdown representation of the PDF content matching the
    /// supplied flag combination.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="pdfBytes"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="pdfBytes"/> is empty or exceeds the configured maximum size.</exception>
    /// <exception cref="PdfExtractionException">If export fails inside the FFI.</exception>
    /// <exception cref="OperationCanceledException">If the operation is cancelled.</exception>
    public Task<string> ToMarkdownAsync(
        byte[] pdfBytes,
        MarkdownOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        var optionsJson = options.ToJson();
        return Task.Run(() => MarkdownWithOptions(pdfBytes, optionsJson), cancellationToken);
    }

    /// <summary>
    /// Export PDF content in contextual format (optimized for LLM context windows).
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Contextual representation of the PDF content.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If export fails.</exception>
    public Task<string> ToContextualAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => StructuredExport(pdfBytes, NativeMethods.oxidize_to_contextual, "contextual"), cancellationToken);
    }

    /// <summary>
    /// Export PDF content as structured JSON.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON representation of the PDF content.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If export fails.</exception>
    public Task<string> ToJsonAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => StructuredExport(pdfBytes, NativeMethods.oxidize_to_json, "JSON"), cancellationToken);
    }

    /// <summary>
    /// Extract plain text from PDF bytes using custom extraction options.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="options">Extraction options controlling layout, columns, hyphenation, etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted plain text.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes or options is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If extraction fails.</exception>
    public Task<string> ExtractTextAsync(byte[] pdfBytes, ExtractionOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        options ??= new ExtractionOptions();
        options.Validate();

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ExtractTextWithOptions(pdfBytes, options), cancellationToken);
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
#pragma warning disable CS0618 // Legacy ChunkOptions kept callable for one minor release.
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
#pragma warning restore CS0618

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
#pragma warning disable CS0618 // Legacy ChunkOptions kept callable for one minor release.
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
#pragma warning restore CS0618

    /// <summary>
    /// Checks if a PDF is encrypted.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the PDF is encrypted; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If parsing fails.</exception>
    public Task<bool> IsEncryptedAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => IsEncrypted(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Tries to unlock an encrypted PDF with the given password.
    /// Returns <c>true</c> if the password is correct (or if the PDF is not encrypted).
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="password">The password to try.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if unlocking succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes or password is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If parsing fails.</exception>
    public Task<bool> UnlockWithPasswordAsync(byte[] pdfBytes, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(password);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => UnlockWithPassword(pdfBytes, password), cancellationToken);
    }

    /// <summary>
    /// Gets the PDF version string (e.g., "1.4", "1.7").
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The PDF version string.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If parsing fails.</exception>
    public Task<string> GetPdfVersionAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetPdfVersion(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Extract document metadata from a PDF (Info dictionary, version, page count).
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PdfMetadata"/> instance with the extracted metadata.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If extraction fails.</exception>
    public Task<PdfMetadata> ExtractMetadataAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => ExtractMetadata(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Check whether a PDF document contains any digital signature fields.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if at least one signature field is present; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If the native library call fails.</exception>
    public Task<bool> HasDigitalSignaturesAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => HasDigitalSignatures(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Extract all digital signature fields from a PDF document.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="DigitalSignature"/> instances with field metadata and signer info.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If the native library call fails.</exception>
    public Task<List<DigitalSignature>> GetDigitalSignaturesAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetDigitalSignatures(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Verify all digital signatures in a PDF and return detailed verification results.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="SignatureVerificationResult"/> with hash, signature, and certificate validation.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If the native library call fails.</exception>
    public Task<List<SignatureVerificationResult>> VerifySignaturesAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => VerifySignatures(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Check whether a PDF document contains any interactive form fields (AcroForm widgets).
    /// </summary>
    /// <remarks>
    /// Calling this method followed by <see cref="GetFormFieldsAsync"/> will parse the PDF twice.
    /// If you need both results, call <see cref="GetFormFieldsAsync"/> directly and check
    /// whether the returned list is empty.
    /// </remarks>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if at least one form field is present; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If the native library call fails.</exception>
    public Task<bool> HasFormFieldsAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => HasFormFields(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Extract all interactive form fields from a PDF document.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of <see cref="FormField"/> instances with field metadata, values, and options.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If the native library call fails.</exception>
    public Task<List<FormField>> GetFormFieldsAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetFormFields(pdfBytes), cancellationToken);
    }

    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="PdfExtractionException">If extraction fails.</exception>
    public Task<List<PdfAnnotation>> GetAnnotationsAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetAnnotations(pdfBytes), cancellationToken);
    }

    /// <summary>
    /// Get the resources for a specific page (fonts, images, resource keys).
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PageResources"/> instance with the page's resources.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber is less than 1.</exception>
    /// <exception cref="PdfExtractionException">If extraction fails.</exception>
    public Task<PageResources> GetPageResourcesAsync(byte[] pdfBytes, int pageNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1 (1-based indexing)");
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetPageResources(pdfBytes, pageNumber), cancellationToken);
    }

    /// <summary>
    /// Get the raw content streams for a specific page as decoded byte arrays.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of decoded content stream byte arrays.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber is less than 1.</exception>
    /// <exception cref="PdfExtractionException">If extraction fails.</exception>
    public Task<PageContentStreams> GetPageContentStreamAsync(byte[] pdfBytes, int pageNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1 (1-based indexing)");
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetPageContentStream(pdfBytes, pageNumber), cancellationToken);
    }

    /// <summary>
    /// Analyze a page's content to determine if it's text-based, scanned, or mixed.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ContentAnalysis"/> with the page's content classification.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber is less than 1.</exception>
    /// <exception cref="PdfExtractionException">If analysis fails.</exception>
    public Task<ContentAnalysis> AnalyzePageContentAsync(byte[] pdfBytes, int pageNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1 (1-based indexing)");
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => AnalyzePageContent(pdfBytes, pageNumber), cancellationToken);
    }

    /// <summary>
    /// Gets the dimensions of a specific page from a parsed PDF.
    /// </summary>
    /// <param name="pdfBytes">PDF file content as byte array.</param>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple with the page width and height in PDF points.</returns>
    /// <exception cref="ArgumentNullException">If pdfBytes is null.</exception>
    /// <exception cref="ArgumentException">If pdfBytes is empty or exceeds maximum size.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If pageNumber is less than 1.</exception>
    /// <exception cref="PdfExtractionException">If parsing fails.</exception>
    public Task<(double Width, double Height)> GetPageDimensionsAsync(
        byte[] pdfBytes,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1 (1-based indexing)");
        ValidatePdfSize(pdfBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => GetPageDimensions(pdfBytes, pageNumber), cancellationToken);
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

    // ── FFI helpers ──────────────────────────────────────────────────────────

    private delegate int NativeJsonCall(IntPtr pdfBytes, nuint pdfLen, out IntPtr outJson);
    private delegate int NativeJsonCallWithProfile(IntPtr pdfBytes, nuint pdfLen, byte profile, out IntPtr outJson);
    private delegate int NativeJsonCallWithConfig(IntPtr pdfBytes, nuint pdfLen, string configJson, out IntPtr outJson);
    private delegate int NativeStringCall(IntPtr pdfBytes, nuint pdfLen, out IntPtr outText);

    private static T WithPinnedPdf<T>(byte[] pdfBytes, Func<IntPtr, nuint, T> action)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);
            return action(pdfPtr, (nuint)pdfBytes.Length);
        }
        finally
        {
            if (pdfPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(pdfPtr);
        }
    }

    private static T CallNativeJson<T>(byte[] pdfBytes, NativeJsonCall nativeCall, string errorMsg) where T : class, new()
    {
        return WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var result = nativeCall(ptr, len, out jsonPtr);
                ThrowIfError(result, errorMsg);
                // Use "[]" as fallback: List<T> types need a JSON array, not object.
                // This path only triggers if Rust returns Success without setting out_json.
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });
    }

    private static T CallNativeJsonWithProfile<T>(
        byte[] pdfBytes,
        byte profile,
        NativeJsonCallWithProfile nativeCall,
        string errorMsg) where T : class, new()
    {
        return WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var result = nativeCall(ptr, len, profile, out jsonPtr);
                ThrowIfError(result, errorMsg);
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });
    }

    private static T CallNativeJsonWithConfig<T>(
        byte[] pdfBytes,
        string configJson,
        NativeJsonCallWithConfig nativeCall,
        string errorMsg) where T : class, new()
    {
        return WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var result = nativeCall(ptr, len, configJson, out jsonPtr);
                ThrowIfError(result, errorMsg);
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });
    }

    private static string CallNativeString(byte[] pdfBytes, NativeStringCall nativeCall, string errorMsg)
    {
        return WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr textPtr = IntPtr.Zero;
            try
            {
                var result = nativeCall(ptr, len, out textPtr);
                ThrowIfError(result, errorMsg);
                return Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
            }
            finally
            {
                if (textPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(textPtr);
            }
        });
    }

    // ── Private FFI method implementations ───────────────────────────────────

    private string ExtractText(byte[] pdfBytes) =>
        CallNativeString(pdfBytes, NativeMethods.oxidize_extract_text, "Failed to extract text from PDF");

#pragma warning disable CS0618 // Legacy ChunkOptions kept callable for one minor release.
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
#pragma warning restore CS0618

    private int GetPageCount(byte[] pdfBytes) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            var result = NativeMethods.oxidize_get_page_count(ptr, len, out var pageCount);
            ThrowIfError(result, "Failed to get page count from PDF");
            return (int)pageCount;
        });

    private string ExtractTextFromPage(byte[] pdfBytes, int pageNumber) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr textPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.oxidize_extract_text_from_page(ptr, len, (nuint)pageNumber, out textPtr);
                ThrowIfError(result, $"Failed to extract text from page {pageNumber}");
                return Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
            }
            finally
            {
                if (textPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(textPtr);
            }
        });

#pragma warning disable CS0618 // Legacy ChunkOptions kept callable for one minor release.
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
#pragma warning restore CS0618

    private bool IsEncrypted(byte[] pdfBytes) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            var result = NativeMethods.oxidize_is_encrypted(ptr, len, out var encrypted);
            ThrowIfError(result, "Failed to check if PDF is encrypted");
            return encrypted;
        });

    private bool UnlockWithPassword(byte[] pdfBytes, string password) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            var result = NativeMethods.oxidize_unlock_pdf(ptr, len, password, out var unlocked);
            ThrowIfError(result, "Failed to unlock PDF");
            return unlocked;
        });

    private string GetPdfVersion(byte[] pdfBytes) =>
        CallNativeString(pdfBytes, NativeMethods.oxidize_get_pdf_version, "Failed to get PDF version");

    private List<PdfElement> Partition(byte[] pdfBytes) =>
        CallNativeJson<List<PdfElement>>(pdfBytes, NativeMethods.oxidize_partition, "Failed to partition PDF");

    private List<PdfElement> PartitionWithProfile(byte[] pdfBytes, ExtractionProfile profile) =>
        CallNativeJsonWithProfile<List<PdfElement>>(
            pdfBytes,
            (byte)profile,
            NativeMethods.oxidize_partition_with_profile,
            $"Failed to partition PDF with profile {profile}");

    private List<PdfElement> PartitionWithConfig(byte[] pdfBytes, string configJson) =>
        CallNativeJsonWithConfig<List<PdfElement>>(
            pdfBytes,
            configJson,
            NativeMethods.oxidize_partition_with_config,
            "Failed to partition PDF with explicit PartitionConfig");

    private List<RagChunk> ExtractRagChunks(byte[] pdfBytes) =>
        CallNativeJson<List<RagChunk>>(pdfBytes, NativeMethods.oxidize_rag_chunks, "Failed to extract RAG chunks");

    private List<RagChunk> RagChunksWithProfile(byte[] pdfBytes, ExtractionProfile profile) =>
        CallNativeJsonWithProfile<List<RagChunk>>(
            pdfBytes,
            (byte)profile,
            NativeMethods.oxidize_rag_chunks_with_profile,
            $"Failed to extract RAG chunks with profile {profile}");

    private List<RagChunk> RagChunksWithConfigs(byte[] pdfBytes, string? partitionJson, string? hybridJson) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var rc = NativeMethods.oxidize_rag_chunks_with_config(
                    ptr, len, partitionJson, hybridJson, out jsonPtr);
                ThrowIfError(rc, "Failed to extract RAG chunks with explicit configs");
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                return JsonSerializer.Deserialize<List<RagChunk>>(json) ?? new();
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });

    private List<SemanticChunk> SemanticChunksImpl(byte[] pdfBytes, string? partitionJson, string semanticJson) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var rc = NativeMethods.oxidize_semantic_chunks(
                    ptr, len, partitionJson, semanticJson, out jsonPtr);
                ThrowIfError(rc, "Failed to extract semantic chunks");
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                return JsonSerializer.Deserialize<List<SemanticChunk>>(json) ?? new();
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });

    private string MarkdownWithOptions(byte[] pdfBytes, string optionsJson) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr textPtr = IntPtr.Zero;
            try
            {
                var rc = NativeMethods.oxidize_to_markdown_with_options(
                    ptr, len, optionsJson, out textPtr);
                ThrowIfError(rc, "Failed to export PDF as markdown with explicit options");
                return Marshal.PtrToStringUTF8(textPtr) ?? string.Empty;
            }
            finally
            {
                if (textPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(textPtr);
            }
        });

    private string StructuredExport(byte[] pdfBytes, NativeStringCall nativeFunc, string formatName) =>
        CallNativeString(pdfBytes, nativeFunc, $"Failed to export PDF as {formatName}");

    private string ExtractTextWithOptions(byte[] pdfBytes, ExtractionOptions options)
    {
        IntPtr pdfPtr = IntPtr.Zero;
        IntPtr textPtr = IntPtr.Zero;

        try
        {
            pdfPtr = Marshal.AllocHGlobal(pdfBytes.Length);
            Marshal.Copy(pdfBytes, 0, pdfPtr, pdfBytes.Length);

            var nativeOptions = new NativeMethods.ExtractionOptionsNative
            {
                PreserveLayout = options.PreserveLayout,
                SpaceThreshold = options.SpaceThreshold,
                NewlineThreshold = options.NewlineThreshold,
                SortByPosition = options.SortByPosition,
                DetectColumns = options.DetectColumns,
                ColumnThreshold = options.ColumnThreshold,
                MergeHyphenated = options.MergeHyphenated
            };

            var result = NativeMethods.oxidize_extract_text_with_options(
                pdfPtr,
                (nuint)pdfBytes.Length,
                ref nativeOptions,
                out textPtr);

            ThrowIfError(result, "Failed to extract text with options");

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

    private bool HasDigitalSignatures(byte[] pdfBytes) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            var result = NativeMethods.oxidize_has_signatures(ptr, len, out var hasSignatures);
            ThrowIfError(result, "Failed to check for digital signatures");
            return hasSignatures;
        });

    private bool HasFormFields(byte[] pdfBytes) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            var result = NativeMethods.oxidize_has_form_fields(ptr, len, out var hasFields);
            ThrowIfError(result, "Failed to check for form fields");
            return hasFields;
        });

    private List<FormField> GetFormFields(byte[] pdfBytes) =>
        CallNativeJson<List<FormField>>(pdfBytes, NativeMethods.oxidize_get_form_fields, "Failed to get form fields");

    private List<DigitalSignature> GetDigitalSignatures(byte[] pdfBytes) =>
        CallNativeJson<List<DigitalSignature>>(pdfBytes, NativeMethods.oxidize_get_signatures, "Failed to get digital signatures");

    private List<SignatureVerificationResult> VerifySignatures(byte[] pdfBytes) =>
        CallNativeJson<List<SignatureVerificationResult>>(pdfBytes, NativeMethods.oxidize_verify_signatures, "Failed to verify signatures");

    private PdfMetadata ExtractMetadata(byte[] pdfBytes) =>
        CallNativeJson<PdfMetadata>(pdfBytes, NativeMethods.oxidize_get_metadata, "Failed to extract metadata from PDF");

    private List<PdfAnnotation> GetAnnotations(byte[] pdfBytes) =>
        CallNativeJson<List<PdfAnnotation>>(pdfBytes, NativeMethods.oxidize_get_annotations, "Failed to get annotations from PDF");

    private ContentAnalysis AnalyzePageContent(byte[] pdfBytes, int pageNumber) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.oxidize_analyze_page_content(ptr, len, (nuint)pageNumber, out jsonPtr);
                ThrowIfError(result, $"Failed to analyze content for page {pageNumber}");
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<ContentAnalysis>(json) ?? new ContentAnalysis();
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });

    private PageResources GetPageResources(byte[] pdfBytes, int pageNumber) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.oxidize_get_page_resources(ptr, len, (nuint)pageNumber, out jsonPtr);
                ThrowIfError(result, $"Failed to get resources for page {pageNumber}");
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<PageResources>(json) ?? new PageResources();
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });

    private PageContentStreams GetPageContentStream(byte[] pdfBytes, int pageNumber) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            IntPtr jsonPtr = IntPtr.Zero;
            try
            {
                var result = NativeMethods.oxidize_get_page_content_stream(ptr, len, (nuint)pageNumber, out jsonPtr);
                ThrowIfError(result, $"Failed to get content streams for page {pageNumber}");
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{\"streams\":[]}";
                var streamResult = JsonSerializer.Deserialize<ContentStreamResult>(json) ?? new ContentStreamResult();
                var decoded = streamResult.Streams.Select(Convert.FromBase64String).ToList();
                return new PageContentStreams(decoded);
            }
            finally
            {
                if (jsonPtr != IntPtr.Zero)
                    NativeMethods.oxidize_free_string(jsonPtr);
            }
        });

    private (double Width, double Height) GetPageDimensions(byte[] pdfBytes, int pageNumber) =>
        WithPinnedPdf(pdfBytes, (ptr, len) =>
        {
            var result = NativeMethods.oxidize_get_page_dimensions(ptr, len, (nuint)pageNumber, out var width, out var height);
            ThrowIfError(result, $"Failed to get dimensions for page {pageNumber}");
            return (width, height);
        });

    internal static void ThrowIfError(int errorCode, string message)
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
