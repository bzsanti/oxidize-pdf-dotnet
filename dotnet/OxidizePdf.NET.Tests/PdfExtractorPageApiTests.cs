using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for page-by-page extraction API.
/// </summary>
public class PdfExtractorPageApiTests
{
    #region GetPageCountAsync Tests

    [Fact]
    public async Task GetPageCountAsync_SamplePdf_ReturnsPositiveCount()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var pageCount = await extractor.GetPageCountAsync(pdf);

        // Assert
        Assert.True(pageCount >= 1, "Sample PDF should have at least 1 page");
    }

    [Fact]
    public async Task GetPageCountAsync_WithNullBytes_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new PdfExtractor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.GetPageCountAsync(null!));
    }

    [Fact]
    public async Task GetPageCountAsync_WithEmptyBytes_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.GetPageCountAsync([]));
    }

    [Fact]
    public async Task GetPageCountAsync_WithCancellation_ThrowsOperationCanceled()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.GetPageCountAsync(pdf, cts.Token));
    }

    #endregion

    #region ExtractTextFromPageAsync Tests

    [Fact]
    public async Task ExtractTextFromPageAsync_FirstPage_ReturnsText()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var text = await extractor.ExtractTextFromPageAsync(pdf, 1);

        // Assert
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_PageZero_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => extractor.ExtractTextFromPageAsync(pdf, 0));
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_NegativePage_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => extractor.ExtractTextFromPageAsync(pdf, -1));
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_PageBeyondCount_ThrowsPdfExtractionException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var pageCount = await extractor.GetPageCountAsync(pdf);

        // Act & Assert - page beyond count should fail in the native layer
        var ex = await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.ExtractTextFromPageAsync(pdf, pageCount + 100));
        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithNullBytes_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new PdfExtractor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ExtractTextFromPageAsync(null!, 1));
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithCancellation_ThrowsOperationCanceled()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTextFromPageAsync(pdf, 1, cts.Token));
    }

    #endregion

    #region ExtractChunksFromPageAsync Tests

    [Fact]
    public async Task ExtractChunksFromPageAsync_FirstPage_ReturnsChunks()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var chunks = await extractor.ExtractChunksFromPageAsync(pdf, 1);

        // Assert
        Assert.NotNull(chunks);
        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.Equal(1, chunk.PageNumber));
    }

    [Fact]
    public async Task ExtractChunksFromPageAsync_WithOptions_AppliesOptions()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = 100, Overlap = 10 };

        // Act
        var chunks = await extractor.ExtractChunksFromPageAsync(pdf, 1, options);

        // Assert
        Assert.NotNull(chunks);
        Assert.All(chunks, chunk =>
        {
            Assert.True(chunk.Text.Length <= 100 || !chunk.Text.Contains(" "),
                "Chunk should respect max size unless no word breaks");
        });
    }

    [Fact]
    public async Task ExtractChunksFromPageAsync_PageZero_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => extractor.ExtractChunksFromPageAsync(pdf, 0));
    }

    [Fact]
    public async Task ExtractChunksFromPageAsync_PageBeyondCount_ThrowsPdfExtractionException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var pageCount = await extractor.GetPageCountAsync(pdf);

        // Act & Assert - page beyond count should fail in the native layer
        var ex = await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.ExtractChunksFromPageAsync(pdf, pageCount + 100));
        Assert.Contains("out of range", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksFromPageAsync_WithNullBytes_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new PdfExtractor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ExtractChunksFromPageAsync(null!, 1));
    }

    [Fact]
    public async Task ExtractChunksFromPageAsync_ChunksHaveSequentialIndexes()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var chunks = await extractor.ExtractChunksFromPageAsync(pdf, 1);

        // Assert
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Index);
        }
    }

    #endregion

    #region Multi-Page Extraction Tests

    [Fact]
    public async Task ExtractTextFromPageAsync_FirstPage_ReturnsNonEmptyText()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var pageText = await extractor.ExtractTextFromPageAsync(pdf, 1);

        // Assert
        Assert.NotNull(pageText);
        Assert.NotEmpty(pageText);
    }

    #endregion
}
