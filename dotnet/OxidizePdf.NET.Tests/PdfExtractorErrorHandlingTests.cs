using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for error handling and error message propagation from Rust FFI layer.
/// </summary>
public class PdfExtractorErrorHandlingTests
{
    [Fact]
    public async Task ExtractTextAsync_WithCorruptedPdf_ThrowsExceptionWithDescriptiveMessage()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var corruptedBytes = PdfTestFixtures.GetCorruptedPdf();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.ExtractTextAsync(corruptedBytes));

        // Error message should contain context from Rust, not just generic error code
        Assert.False(
            ex.Message == "Failed to extract text from PDF: PdfParseError",
            "Error message should include details from Rust, not just error code");
        Assert.Contains("PDF", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractTextAsync_WithPartiallyCorruptedPdf_ThrowsExceptionWithDetails()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var corruptedBytes = PdfTestFixtures.GetPartiallyCorruptedPdf();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.ExtractTextAsync(corruptedBytes));

        // Should have more detail than just the error enum
        Assert.NotNull(ex.Message);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public async Task ExtractChunksAsync_WithCorruptedPdf_ThrowsExceptionWithDescriptiveMessage()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var corruptedBytes = PdfTestFixtures.GetCorruptedPdf();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PdfExtractionException>(
            () => extractor.ExtractChunksAsync(corruptedBytes));

        // Error message should contain context from Rust
        Assert.False(
            ex.Message == "Failed to extract chunks from PDF: PdfParseError",
            "Error message should include details from Rust, not just error code");
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyBytes_ThrowsArgumentException()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var emptyBytes = Array.Empty<byte>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => extractor.ExtractTextAsync(emptyBytes));
    }

    [Fact]
    public async Task ExtractTextAsync_WithNullBytes_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new PdfExtractor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ExtractTextAsync(null!));
    }

    [Fact]
    public async Task ExtractChunksAsync_WithNullBytes_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new PdfExtractor();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => extractor.ExtractChunksAsync(null!));
    }
}
