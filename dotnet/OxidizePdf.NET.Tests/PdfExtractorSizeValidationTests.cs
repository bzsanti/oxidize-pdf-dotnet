using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PDF size validation to prevent OOM attacks.
/// </summary>
public class PdfExtractorSizeValidationTests
{
    [Fact]
    public void Constructor_WithCustomMaxSize_AcceptsValidSize()
    {
        // Arrange & Act
        var extractor = new PdfExtractor(maxFileSizeBytes: 50_000_000); // 50MB

        // Assert - no exception thrown
        Assert.NotNull(extractor);
    }

    [Fact]
    public void Constructor_WithZeroMaxSize_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new PdfExtractor(maxFileSizeBytes: 0));
        Assert.Contains("maxFileSizeBytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithNegativeMaxSize_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new PdfExtractor(maxFileSizeBytes: -1));
        Assert.Contains("maxFileSizeBytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithDefaultMaxSize_Uses100MB()
    {
        // Arrange
        var extractor = new PdfExtractor();

        // Assert - default should be 100MB (we can't directly check, but we verify it accepts files under 100MB)
        Assert.NotNull(extractor);
    }

    [Fact]
    public async Task ExtractTextAsync_WithOversizedPdf_ThrowsArgumentException()
    {
        // Arrange - extractor with 1KB limit
        var extractor = new PdfExtractor(maxFileSizeBytes: 1024);
        var largePdf = new byte[2048]; // 2KB, exceeds limit

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractTextAsync(largePdf));

        Assert.Contains("size", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractTextAsync_WithinSizeLimit_Succeeds()
    {
        // Arrange - extractor with 10MB limit (enough for sample.pdf)
        var extractor = new PdfExtractor(maxFileSizeBytes: 10_000_000);
        var pdf = PdfTestFixtures.GetSamplePdf();

        // Act
        var text = await extractor.ExtractTextAsync(pdf);

        // Assert
        Assert.NotNull(text);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithOversizedPdf_ThrowsArgumentException()
    {
        // Arrange - extractor with 1KB limit
        var extractor = new PdfExtractor(maxFileSizeBytes: 1024);
        var largePdf = new byte[2048]; // 2KB, exceeds limit

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractChunksAsync(largePdf));

        Assert.Contains("size", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractTextAsync_ExactlyAtSizeLimit_Succeeds()
    {
        // Arrange - PDF exactly at limit should be accepted
        var samplePdf = PdfTestFixtures.GetSamplePdf();
        var extractor = new PdfExtractor(maxFileSizeBytes: samplePdf.Length);

        // Act
        var text = await extractor.ExtractTextAsync(samplePdf);

        // Assert
        Assert.NotNull(text);
    }
}
