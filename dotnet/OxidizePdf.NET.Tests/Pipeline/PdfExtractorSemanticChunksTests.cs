using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests.Pipeline;

/// <summary>
/// Tests for <see cref="PdfExtractor.SemanticChunksAsync"/> introduced in Task 16.
/// Wraps <c>oxidize_semantic_chunks</c>; uses the element-boundary-aware
/// chunker (preserves structural unity for titles/tables/code blocks).
/// </summary>
public class PdfExtractorSemanticChunksTests
{
    [Fact]
    public async Task SemanticChunksAsync_with_defaults_returns_chunks_with_full_schema()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.SemanticChunksAsync(pdf);

        Assert.NotEmpty(chunks);
        // Every documented SemanticChunk field must be populated/typed correctly.
        for (int i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            Assert.Equal(i, c.ChunkIndex);
            Assert.False(string.IsNullOrEmpty(c.Text), "Text must be non-empty");
            Assert.True(c.TokenEstimate >= 0);
            Assert.NotNull(c.PageNumbers);
            Assert.All(c.PageNumbers, p => Assert.True(p >= 1, $"page numbers must be 1-based, got {p}"));
        }
    }

    [Fact]
    public async Task SemanticChunksAsync_with_explicit_config_succeeds()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.SemanticChunksAsync(
            pdf,
            new SemanticChunkConfig(64).WithOverlap(8));

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task SemanticChunksAsync_with_partition_config_succeeds()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.SemanticChunksAsync(
            pdf,
            config: new SemanticChunkConfig(128),
            partitionConfig: new PartitionConfig().WithReadingOrder(ReadingOrderStrategy.XyCut(15.0)));

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task SemanticChunksAsync_smaller_max_tokens_produces_at_least_as_many_chunks()
    {
        // Same monotonicity invariant as the hybrid-chunker test: shrinking
        // max_tokens cannot reduce chunk count for the same input.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var big = await extractor.SemanticChunksAsync(pdf, new SemanticChunkConfig(2048));
        var tiny = await extractor.SemanticChunksAsync(pdf, new SemanticChunkConfig(8).WithOverlap(0));

        Assert.True(
            tiny.Count >= big.Count,
            $"tiny max_tokens=8 produced {tiny.Count} chunks; big max_tokens=2048 produced {big.Count}.");
    }

    [Fact]
    public async Task SemanticChunksAsync_validates_overlap_ge_max()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var bad = new SemanticChunkConfig { MaxTokens = 10, OverlapTokens = 10 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.SemanticChunksAsync(pdf, bad));
    }

    [Fact]
    public async Task SemanticChunksAsync_validates_zero_max_tokens()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var bad = new SemanticChunkConfig { MaxTokens = 0 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.SemanticChunksAsync(pdf, bad));
    }

    [Fact]
    public async Task SemanticChunksAsync_validates_PartitionConfig()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.SemanticChunksAsync(
                pdf,
                config: new SemanticChunkConfig(64),
                partitionConfig: new PartitionConfig { HeaderZone = 2.0 }));
    }

    [Fact]
    public async Task SemanticChunksAsync_null_bytes_throws_ArgumentNullException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.SemanticChunksAsync(null!));
    }

    [Fact]
    public async Task SemanticChunksAsync_empty_bytes_throws_ArgumentException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.SemanticChunksAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task SemanticChunksAsync_respects_cancellation()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.SemanticChunksAsync(pdf, cancellationToken: cts.Token));
    }
}
