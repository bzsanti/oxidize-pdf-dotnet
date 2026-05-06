using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests.Pipeline;

/// <summary>
/// Tests for the <see cref="PdfExtractor.RagChunksAsync(byte[], ExtractionProfile, System.Threading.CancellationToken)"/>
/// overload introduced in Task 14 (RAG-003 / RAG-005). Wraps
/// <c>oxidize_rag_chunks_with_profile</c>; same byte discriminant contract
/// as the partition+profile overload from Task 12.
/// </summary>
public class PdfExtractorRagProfileTests
{
    [Fact]
    public async Task RagChunksAsync_with_Rag_profile_returns_chunks_with_full_schema()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.RagChunksAsync(pdf, ExtractionProfile.Rag);

        Assert.NotEmpty(chunks);
        // Every documented RagChunk field must be populated/typed.
        Assert.All(chunks, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.Text));
            Assert.False(string.IsNullOrEmpty(c.FullText));
            // FullText is text + heading context, so length >= text length.
            Assert.True(
                c.FullText.Length >= c.Text.Length,
                $"FullText.Length ({c.FullText.Length}) must be >= Text.Length ({c.Text.Length})");
            Assert.True(c.TokenEstimate >= 0);
            Assert.NotNull(c.PageNumbers);
            Assert.NotNull(c.ElementTypes);
            Assert.All(c.PageNumbers, p => Assert.True(p >= 1, $"page numbers must be 1-based, got {p}"));
        });
    }

    [Fact]
    public async Task RagChunksAsync_with_profile_emits_sequential_chunk_indexes()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.RagChunksAsync(pdf, ExtractionProfile.Standard);

        for (int i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].ChunkIndex);
    }

    [Theory]
    [InlineData(ExtractionProfile.Standard)]
    [InlineData(ExtractionProfile.Academic)]
    [InlineData(ExtractionProfile.Form)]
    [InlineData(ExtractionProfile.Government)]
    [InlineData(ExtractionProfile.Dense)]
    [InlineData(ExtractionProfile.Presentation)]
    [InlineData(ExtractionProfile.Rag)]
    public async Task RagChunksAsync_accepts_every_declared_profile(ExtractionProfile profile)
    {
        // Exhaustive: same guard as the partition overload — every C# enum
        // value must round-trip through the FFI. Catches a silent reorder
        // of the upstream enum.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.RagChunksAsync(pdf, profile);

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task RagChunksAsync_with_invalid_discriminant_throws_PdfExtractionException()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.RagChunksAsync(pdf, (ExtractionProfile)200));
    }

    [Fact]
    public async Task RagChunksAsync_with_profile_null_bytes_throws_ArgumentNullException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.RagChunksAsync(null!, ExtractionProfile.Standard));
    }

    [Fact]
    public async Task RagChunksAsync_with_profile_empty_bytes_throws_ArgumentException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.RagChunksAsync(Array.Empty<byte>(), ExtractionProfile.Standard));
    }

    [Fact]
    public async Task RagChunksAsync_with_profile_respects_cancellation()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.RagChunksAsync(pdf, ExtractionProfile.Standard, cts.Token));
    }
}
