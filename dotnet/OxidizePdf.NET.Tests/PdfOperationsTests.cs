namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfOperations static class — input validation and integration.
/// </summary>
public class PdfOperationsTests
{
    // ── SplitAsync validation ────────────────────────────────────────────

    [Fact]
    public async Task SplitAsync_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.SplitAsync(null!));
    }

    [Fact]
    public async Task SplitAsync_WithEmptyBytes_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfOperations.SplitAsync(Array.Empty<byte>()));
    }

    // ── MergeAsync validation ────────────────────────────────────────────

    [Fact]
    public async Task MergeAsync_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.MergeAsync(null!));
    }

    [Fact]
    public async Task MergeAsync_WithEmptyList_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfOperations.MergeAsync(Array.Empty<byte[]>()));
    }

    // ── RotateAsync validation ───────────────────────────────────────────

    [Fact]
    public async Task RotateAsync_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.RotateAsync(null!, 90));
    }

    [Fact]
    public async Task RotateAsync_WithEmptyBytes_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfOperations.RotateAsync(Array.Empty<byte>(), 90));
    }

    // ── ExtractPagesAsync validation ─────────────────────────────────────

    [Fact]
    public async Task ExtractPagesAsync_WithNullBytes_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.ExtractPagesAsync(null!, new[] { 0 }));
    }

    [Fact]
    public async Task ExtractPagesAsync_WithEmptyBytes_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfOperations.ExtractPagesAsync(Array.Empty<byte>(), new[] { 0 }));
    }

    [Fact]
    public async Task ExtractPagesAsync_WithNullIndices_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.ExtractPagesAsync(new byte[] { 1 }, null!));
    }

    [Fact]
    public async Task ExtractPagesAsync_WithEmptyIndices_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfOperations.ExtractPagesAsync(new byte[] { 1 }, Array.Empty<int>()));
    }

    // ── Integration tests (require native library) ──────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SplitAsync_WithValidPdf_ReturnsSplitPages()
    {
        var pdfBytes = CreateSimpleMultiPagePdf();
        var pages = await PdfOperations.SplitAsync(pdfBytes);
        Assert.NotNull(pages);
        Assert.Equal(2, pages.Count);
        foreach (var page in pages)
        {
            Assert.True(page.Length > 0);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MergeAsync_WithValidPdfs_ReturnsMergedPdf()
    {
        var pdf1 = CreateSimplePdf("Page 1");
        var pdf2 = CreateSimplePdf("Page 2");
        var merged = await PdfOperations.MergeAsync(new[] { pdf1, pdf2 });
        Assert.NotNull(merged);
        Assert.True(merged.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RotateAsync_With90Degrees_ReturnsRotatedPdf()
    {
        var pdfBytes = CreateSimplePdf("Rotate me");
        var rotated = await PdfOperations.RotateAsync(pdfBytes, 90);
        Assert.NotNull(rotated);
        Assert.True(rotated.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExtractPagesAsync_WithValidPdf_ReturnsExtractedPages()
    {
        var pdfBytes = CreateSimpleMultiPagePdf();
        var extracted = await PdfOperations.ExtractPagesAsync(pdfBytes, new[] { 0 });
        Assert.NotNull(extracted);
        Assert.True(extracted.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SplitAsync_SupportsCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PdfOperations.SplitAsync(new byte[] { 1 }, cts.Token));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static byte[] CreateSimplePdf(string text)
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12).TextAt(72, 700, text);
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    private static byte[] CreateSimpleMultiPagePdf()
    {
        using var doc = new PdfDocument();
        using var page1 = PdfPage.A4();
        page1.SetFont(StandardFont.Helvetica, 12).TextAt(72, 700, "Page 1");
        doc.AddPage(page1);

        using var page2 = PdfPage.A4();
        page2.SetFont(StandardFont.Helvetica, 12).TextAt(72, 700, "Page 2");
        doc.AddPage(page2);

        return doc.SaveToBytes();
    }
}
