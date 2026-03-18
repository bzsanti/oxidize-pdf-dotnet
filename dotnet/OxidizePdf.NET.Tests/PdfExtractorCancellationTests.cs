using OxidizePdf.NET.Models;
using OxidizePdf.NET.Tests.TestHelpers;

namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for CancellationToken support in PdfExtractor.
/// </summary>
public class PdfExtractorCancellationTests
{
    private static CancellationToken CancelledToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        return cts.Token;
    }

    // ── Existing cancellation tests ──────────────────────────────────────────

    [Fact]
    public async Task ExtractTextAsync_WithAlreadyCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTextAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task ExtractChunksAsync_WithAlreadyCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractChunksAsync(pdf, cancellationToken: CancelledToken()));
    }

    [Fact]
    public async Task ExtractTextAsync_WithValidToken_Succeeds()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetSamplePdf();
        var cts = new CancellationTokenSource();

        var text = await extractor.ExtractTextAsync(pdf, cts.Token);

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task ExtractTextAsync_CancellationBeforeValidation_ThrowsEarly()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTextAsync(pdf, CancelledToken()));
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000,
            "Cancellation should fail fast, not wait for processing");
    }

    // ── Phase 2-8 cancellation tests ─────────────────────────────────────────

    [Fact]
    public async Task GetPageCountAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.GetPageCountAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task ExtractTextFromPageAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTextFromPageAsync(pdf, 1, CancelledToken()));
    }

    [Fact]
    public async Task ExtractChunksFromPageAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractChunksFromPageAsync(pdf, 1, cancellationToken: CancelledToken()));
    }

    [Fact]
    public async Task IsEncryptedAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.IsEncryptedAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task UnlockWithPasswordAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.UnlockWithPasswordAsync(pdf, "test", CancelledToken()));
    }

    [Fact]
    public async Task GetPdfVersionAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.GetPdfVersionAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task ExtractMetadataAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractMetadataAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task GetAnnotationsAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.GetAnnotationsAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task GetPageResourcesAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.GetPageResourcesAsync(pdf, 1, CancelledToken()));
    }

    [Fact]
    public async Task GetPageContentStreamAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.GetPageContentStreamAsync(pdf, 1, CancelledToken()));
    }

    [Fact]
    public async Task AnalyzePageContentAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.AnalyzePageContentAsync(pdf, 1, CancelledToken()));
    }

    [Fact]
    public async Task GetPageDimensionsAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.GetPageDimensionsAsync(pdf, 1, CancelledToken()));
    }

    [Fact]
    public async Task PartitionAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.PartitionAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task RagChunksAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.RagChunksAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task ToMarkdownAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ToMarkdownAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task ToContextualAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ToContextualAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task ToJsonAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ToJsonAsync(pdf, CancelledToken()));
    }

    [Fact]
    public async Task ExtractTextAsync_WithOptionsAndCancelledToken_ThrowsOperationCanceled()
    {
        var extractor = new PdfExtractor();
        var pdf = PdfTestFixtures.GetValidSinglePagePdf();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTextAsync(pdf, new ExtractionOptions(), CancelledToken()));
    }
}
