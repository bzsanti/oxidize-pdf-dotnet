using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Functional tests for PdfExtractor text and chunk extraction.
/// </summary>
public class PdfExtractorFunctionalTests
{
    [Fact]
    public async Task ExtractTextAsync_FromSamplePdf_ReturnsNonEmptyText()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var text = await extractor.ExtractTextAsync(pdf);

        // Assert
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractChunksAsync_FromSamplePdf_ReturnsChunks()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions
        {
            MaxChunkSize = 512,
            Overlap = 50,
            PreserveSentenceBoundaries = true
        };

        // Act
        var chunks = await extractor.ExtractChunksAsync(pdf, options);

        // Assert
        Assert.NotNull(chunks);
        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk =>
        {
            Assert.True(chunk.Index >= 0);
            Assert.True(chunk.PageNumber > 0);
            Assert.NotEmpty(chunk.Text);
        });
    }

    [Fact]
    public async Task ExtractChunksAsync_WithDefaultOptions_UsesDefaults()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act - use default options (null)
        var chunks = await extractor.ExtractChunksAsync(pdf);

        // Assert
        Assert.NotNull(chunks);
        // Default options should work
    }

    [Fact]
    public async Task ExtractChunksAsync_ChunksHaveValidMetadata()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var chunks = await extractor.ExtractChunksAsync(pdf);

        // Assert
        Assert.All(chunks, chunk =>
        {
            Assert.True(chunk.Confidence >= 0 && chunk.Confidence <= 1,
                "Confidence should be between 0 and 1");
            Assert.NotNull(chunk.BoundingBox);
        });
    }

    [Fact]
    public async Task ExtractChunksAsync_ChunksAreInOrder()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var chunks = await extractor.ExtractChunksAsync(pdf);

        // Assert - chunks should be indexed sequentially
        for (int i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].Index);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_MultipleTimes_ProducesSameResult()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var text1 = await extractor.ExtractTextAsync(pdf);
        var text2 = await extractor.ExtractTextAsync(pdf);

        // Assert - extraction should be deterministic
        Assert.Equal(text1, text2);
    }

    [Fact]
    public void Version_ReturnsNonEmptyString()
    {
        // Act
        var version = PdfExtractor.Version;

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version);
        Assert.Contains("oxidize", version, StringComparison.OrdinalIgnoreCase);
    }
}
