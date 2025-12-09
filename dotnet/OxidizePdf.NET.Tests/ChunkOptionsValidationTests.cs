using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for ChunkOptions validation.
/// </summary>
public class ChunkOptionsValidationTests
{
    [Fact]
    public async Task ExtractChunksAsync_WithNegativeMaxChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = -1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(pdf, options));
        Assert.Contains("MaxChunkSize", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithZeroMaxChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = 0 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(pdf, options));
        Assert.Contains("MaxChunkSize", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithTooSmallMaxChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = 10 }; // Below minimum of 50

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(pdf, options));
        Assert.Contains("MaxChunkSize", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithTooLargeMaxChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = 100_000 }; // Above maximum

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(pdf, options));
        Assert.Contains("MaxChunkSize", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithNegativeOverlap_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = 512, Overlap = -1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(pdf, options));
        Assert.Contains("Overlap", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithOverlapEqualToMaxChunkSize_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = 512, Overlap = 512 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(pdf, options));
        Assert.Contains("Overlap", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithOverlapGreaterThan50Percent_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = 512, Overlap = 300 }; // > 50%

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(pdf, options));
        Assert.Contains("Overlap", ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithValidOptions_Succeeds()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions
        {
            MaxChunkSize = 512,
            Overlap = 50,
            PreserveSentenceBoundaries = true,
            IncludeMetadata = true
        };

        // Act
        var chunks = await extractor.ExtractChunksAsync(pdf, options);

        // Assert
        Assert.NotNull(chunks);
    }

    [Theory]
    [InlineData(100, 10)]   // Valid: 10% overlap
    [InlineData(256, 25)]   // Valid: ~10% overlap
    [InlineData(1000, 100)] // Valid: 10% overlap
    [InlineData(512, 0)]    // Valid: no overlap
    public async Task ExtractChunksAsync_WithVariousValidOptions_Succeeds(int maxChunkSize, int overlap)
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var options = new ChunkOptions { MaxChunkSize = maxChunkSize, Overlap = overlap };

        // Act
        var chunks = await extractor.ExtractChunksAsync(pdf, options);

        // Assert
        Assert.NotNull(chunks);
    }
}
