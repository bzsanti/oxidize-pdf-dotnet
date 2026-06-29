using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class DecodeEdgeCasesTests
{
    [Fact]
    public async Task DecodeAsync_missing_file_throws_FileNotFound()
    {
        var decoder = new OxidizePdfDecoder();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => decoder.DecodeAsync(Path.Combine(AppContext.BaseDirectory, "does-not-exist.pdf")));
    }

    [Fact]
    public async Task DecodeAsync_empty_bytes_throws_ArgumentException()
    {
        // PdfExtractor.RagChunksAsync rejects empty input with ArgumentException;
        // the decoder must surface it, not swallow it.
        var decoder = new OxidizePdfDecoder();
        await Assert.ThrowsAsync<ArgumentException>(
            () => decoder.DecodeAsync(new BinaryData(Array.Empty<byte>())));
    }
}
