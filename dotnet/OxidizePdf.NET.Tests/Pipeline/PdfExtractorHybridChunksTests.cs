using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests.Pipeline;

/// <summary>
/// Tests for the <see cref="PdfExtractor.RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?, System.Threading.CancellationToken)"/>
/// overload introduced in Task 15. Both configs are independently optional —
/// passing <c>null</c> for either uses the corresponding upstream default.
/// </summary>
public class PdfExtractorHybridChunksTests
{
    [Fact]
    public async Task RagChunksAsync_with_both_null_configs_matches_no_arg_call()
    {
        // Wire-default parity: passing null for both configs must produce
        // the same chunk count as the parameter-less RagChunksAsync(byte[]).
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var defaulted = await extractor.RagChunksAsync(pdf);
        var withNulls = await extractor.RagChunksAsync(pdf, partitionConfig: null, hybridConfig: null);

        Assert.Equal(defaulted.Count, withNulls.Count);
    }

    [Fact]
    public async Task RagChunksAsync_with_only_hybrid_config_uses_partition_default()
    {
        // Common case: tune chunk size while keeping default partitioning.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.RagChunksAsync(
            pdf,
            partitionConfig: null,
            hybridConfig: new HybridChunkConfig().WithMaxTokens(64).WithOverlap(8));

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task RagChunksAsync_with_only_partition_config_uses_hybrid_default()
    {
        // Inverse case: tune partitioner while keeping default chunking.
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.RagChunksAsync(
            pdf,
            partitionConfig: new PartitionConfig().WithReadingOrder(ReadingOrderStrategy.XyCut(15.0)),
            hybridConfig: null);

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task RagChunksAsync_with_both_configs_succeeds()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.RagChunksAsync(
            pdf,
            partitionConfig: new PartitionConfig().WithReadingOrder(ReadingOrderStrategy.Simple),
            hybridConfig: new HybridChunkConfig().WithMaxTokens(128));

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task RagChunksAsync_smaller_max_tokens_produces_at_least_as_many_chunks()
    {
        // Semantic verification of the HybridChunkConfig knob: a tiny
        // max_tokens MUST produce ≥ the chunk count of a generous max_tokens
        // for the same input. Strictly greater on long fixtures; equal-or-greater
        // on the modest sample.pdf (which may already fit in one big chunk).
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var big = await extractor.RagChunksAsync(
            pdf,
            partitionConfig: null,
            hybridConfig: new HybridChunkConfig().WithMaxTokens(2048));
        var tiny = await extractor.RagChunksAsync(
            pdf,
            partitionConfig: null,
            hybridConfig: new HybridChunkConfig().WithMaxTokens(8).WithOverlap(0));

        Assert.True(
            tiny.Count >= big.Count,
            $"tiny max_tokens=8 produced {tiny.Count} chunks; big max_tokens=2048 produced {big.Count}. tiny must be >= big.");
    }

    [Fact]
    public async Task RagChunksAsync_validates_HybridChunkConfig_overlap_ge_max()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var bad = new HybridChunkConfig { MaxTokens = 10, OverlapTokens = 10 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.RagChunksAsync(pdf, null, bad));
    }

    [Fact]
    public async Task RagChunksAsync_validates_HybridChunkConfig_zero_max_tokens()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var bad = new HybridChunkConfig { MaxTokens = 0 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.RagChunksAsync(pdf, null, bad));
    }

    [Fact]
    public async Task RagChunksAsync_validates_PartitionConfig()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();
        var bad = new PartitionConfig { MinTableConfidence = 5.0 };

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.RagChunksAsync(pdf, bad, null));
    }

    [Fact]
    public async Task RagChunksAsync_with_configs_null_bytes_throws_ArgumentNullException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.RagChunksAsync(null!, null, null));
    }

    [Fact]
    public async Task RagChunksAsync_with_configs_empty_bytes_throws_ArgumentException()
    {
        var extractor = new PdfExtractor();

        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.RagChunksAsync(Array.Empty<byte>(), null, null));
    }

    [Fact]
    public async Task RagChunksAsync_with_configs_respects_cancellation()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.RagChunksAsync(pdf, null, null, cts.Token));
    }

    [Fact]
    public async Task RagChunksAsync_returned_chunks_emit_1_based_page_numbers()
    {
        var pdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor();

        var chunks = await extractor.RagChunksAsync(pdf, null, null);

        Assert.All(chunks, c =>
            Assert.All(c.PageNumbers, p =>
                Assert.True(p >= 1, $"page numbers must be 1-based, got {p}")));
    }
}
