using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for RAG pipeline — partition + rag_chunks (Phase 5 — PARSE-010/012).
/// </summary>
public class PdfExtractorPipelineTests
{
    // ── Partition tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task PartitionAsync_ReturnsElements()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf);

        Assert.NotNull(elements);
        Assert.NotEmpty(elements);
    }

    [Fact]
    public async Task PartitionAsync_ElementsHaveValidTypes()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf);

        var validTypes = new HashSet<string>
        {
            "title", "paragraph", "table", "header", "footer",
            "list_item", "image", "code_block", "key_value"
        };

        Assert.All(elements, el =>
        {
            Assert.NotNull(el.ElementType);
            Assert.Contains(el.ElementType, validTypes);
        });
    }

    [Fact]
    public async Task PartitionAsync_ElementsHavePageNumbers()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf);

        Assert.All(elements, el => Assert.True(el.PageNumber >= 1,
            $"Page number should be >= 1, got {el.PageNumber}"));
    }

    [Fact]
    public async Task PartitionAsync_ElementsHaveText()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var elements = await extractor.PartitionAsync(pdf);

        // At least some elements should have text
        Assert.Contains(elements, el => !string.IsNullOrEmpty(el.Text));
    }

    [Fact]
    public async Task PartitionAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.PartitionAsync(null!));
    }

    // ── RagChunks tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task RagChunksAsync_ReturnsChunks()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.NotNull(chunks);
        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task RagChunksAsync_ChunksHaveContent()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.All(chunks, chunk =>
        {
            Assert.NotNull(chunk.Text);
            Assert.NotEmpty(chunk.Text);
        });
    }

    [Fact]
    public async Task RagChunksAsync_ChunksHavePageNumbers()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.All(chunks, chunk =>
        {
            Assert.NotNull(chunk.PageNumbers);
            Assert.NotEmpty(chunk.PageNumbers);
            Assert.All(chunk.PageNumbers, p => Assert.True(p >= 1));
        });
    }

    [Fact]
    public async Task RagChunksAsync_ChunksHaveSequentialIndices()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var chunks = await extractor.RagChunksAsync(pdf);

        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].ChunkIndex);
        }
    }

    [Fact]
    public async Task RagChunksAsync_ChunksHaveTokenEstimate()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.All(chunks, chunk =>
            Assert.True(chunk.TokenEstimate > 0, "Token estimate should be positive"));
    }

    [Fact]
    public async Task RagChunksAsync_ChunksHaveElementTypes()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        var chunks = await extractor.RagChunksAsync(pdf);

        Assert.All(chunks, chunk =>
        {
            Assert.NotNull(chunk.ElementTypes);
            Assert.NotEmpty(chunk.ElementTypes);
        });
    }

    [Fact]
    public async Task RagChunksAsync_NullBytes_ThrowsArgumentNullException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.RagChunksAsync(null!));
    }

    [Fact]
    public async Task RagChunksAsync_EmptyBytes_ThrowsArgumentException()
    {
        var extractor = new PdfExtractor();
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.RagChunksAsync(Array.Empty<byte>()));
    }
}
