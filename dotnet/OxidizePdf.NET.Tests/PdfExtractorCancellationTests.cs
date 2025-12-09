using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for CancellationToken support in PdfExtractor.
/// </summary>
public class PdfExtractorCancellationTests
{
    [Fact]
    public async Task ExtractTextAsync_WithAlreadyCancelledToken_ThrowsOperationCanceled()
    {
        // Arrange - use generated PDF to avoid encoding issues in sample.pdf
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before starting

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTextAsync(pdf, cts.Token));
    }

    [Fact]
    public async Task ExtractChunksAsync_WithAlreadyCancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before starting

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractChunksAsync(pdf, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidToken_Succeeds()
    {
        // Arrange
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var cts = new CancellationTokenSource();

        // Act
        var text = await extractor.ExtractTextAsync(pdf, cts.Token);

        // Assert
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractTextAsync_CancellationBeforeValidation_ThrowsEarly()
    {
        // Arrange - use already cancelled token
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should throw before even validating PDF
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTextAsync(pdf, cts.Token));
        sw.Stop();

        // Should fail quickly (not after processing)
        Assert.True(sw.ElapsedMilliseconds < 1000,
            "Cancellation should fail fast, not wait for processing");
    }
}
