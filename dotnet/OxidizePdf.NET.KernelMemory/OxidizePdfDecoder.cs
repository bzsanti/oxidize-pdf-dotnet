using Microsoft.KernelMemory.DataFormats;
using OxidizePdf.NET;

namespace OxidizePdf.NET.KernelMemory;

/// <summary>
/// Kernel Memory <see cref="IContentDecoder"/> backed by oxidize-pdf. Produces
/// one KM <see cref="Chunk"/> per structure-aware oxidize-pdf RAG chunk.
/// </summary>
public sealed class OxidizePdfDecoder : IContentDecoder
{
    private const string PdfMimeType = "application/pdf";

    private readonly OxidizePdfDecoderOptions _options;
    private readonly PdfExtractor _extractor;

    /// <summary>Creates a decoder. <paramref name="options"/> null uses defaults (RAG profile).</summary>
    public OxidizePdfDecoder(OxidizePdfDecoderOptions? options = null)
    {
        _options = options ?? new OxidizePdfDecoderOptions();
        _extractor = new PdfExtractor();
    }

    /// <inheritdoc />
    public bool SupportsMimeType(string mimeType) =>
        mimeType is not null &&
        mimeType.StartsWith(PdfMimeType, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
