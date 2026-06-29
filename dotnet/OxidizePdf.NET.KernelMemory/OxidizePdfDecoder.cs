using Microsoft.KernelMemory.DataFormats;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;

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
    public async Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filename, cancellationToken).ConfigureAwait(false);
        return await DecodeBytesAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return DecodeBytesAsync(data.ToArray(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var ms = new MemoryStream();
        await data.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return await DecodeBytesAsync(ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<FileContent> DecodeBytesAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        List<RagChunk> chunks = (_options.Partition is not null || _options.Hybrid is not null)
            ? await _extractor.RagChunksAsync(bytes, _options.Partition, _options.Hybrid, cancellationToken).ConfigureAwait(false)
            : await _extractor.RagChunksAsync(bytes, _options.Profile, cancellationToken).ConfigureAwait(false);

        var content = new FileContent(PdfMimeType);
        foreach (var rc in chunks)
        {
            int page = rc.PageNumbers.Count > 0 ? rc.PageNumbers[0] : -1;
            var meta = Chunk.Meta(sentencesAreComplete: true, pageNumber: page);
            content.Sections.Add(new Chunk(rc.FullText, rc.ChunkIndex, meta));
        }

        return content;
    }
}
