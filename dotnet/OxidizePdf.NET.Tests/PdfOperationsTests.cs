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

    // ── ReorderPagesAsync ────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReorderPagesAsync_ReversesOrder_ReturnsValidPdf()
    {
        var pdfBytes = CreateNPagePdf(3);
        var result = await PdfOperations.ReorderPagesAsync(pdfBytes, [2, 1, 0]);
        Assert.NotNull(result);
        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task ReorderPagesAsync_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.ReorderPagesAsync(null!, [0]));
    }

    [Fact]
    public async Task ReorderPagesAsync_EmptyOrder_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => PdfOperations.ReorderPagesAsync(CreateNPagePdf(1), Array.Empty<int>()));
    }

    // ── SwapPagesAsync ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SwapPagesAsync_ReturnsValidPdf()
    {
        var pdfBytes = CreateNPagePdf(3);
        var result = await PdfOperations.SwapPagesAsync(pdfBytes, 0, 2);
        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task SwapPagesAsync_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.SwapPagesAsync(null!, 0, 1));
    }

    // ── MovePageAsync ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MovePageAsync_ReturnsValidPdf()
    {
        var pdfBytes = CreateNPagePdf(3);
        var result = await PdfOperations.MovePageAsync(pdfBytes, 0, 2);
        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task MovePageAsync_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.MovePageAsync(null!, 0, 1));
    }

    // ── ReversePagesAsync ────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReversePagesAsync_ReturnsValidPdf()
    {
        var pdfBytes = CreateNPagePdf(3);
        var result = await PdfOperations.ReversePagesAsync(pdfBytes);
        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task ReversePagesAsync_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.ReversePagesAsync(null!));
    }

    // ── OverlayAsync ─────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OverlayAsync_ReturnsValidPdf()
    {
        var basePdf = CreateNPagePdf(1);
        var overlayPdf = CreateNPagePdf(1);
        var result = await PdfOperations.OverlayAsync(basePdf, overlayPdf);
        Assert.True(result.Length > 100);
    }

    [Fact]
    public async Task OverlayAsync_NullBase_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.OverlayAsync(null!, CreateNPagePdf(1)));
    }

    [Fact]
    public async Task OverlayAsync_NullOverlay_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => PdfOperations.OverlayAsync(CreateNPagePdf(1), null!));
    }

    // ── Negative index validation ──────────────────────────────────────

    [Fact]
    public async Task SwapPagesAsync_NegativePageA_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => PdfOperations.SwapPagesAsync(CreateNPagePdf(2), -1, 0));
    }

    [Fact]
    public async Task SwapPagesAsync_NegativePageB_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => PdfOperations.SwapPagesAsync(CreateNPagePdf(2), 0, -1));
    }

    [Fact]
    public async Task MovePageAsync_NegativeFromIndex_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => PdfOperations.MovePageAsync(CreateNPagePdf(2), -1, 0));
    }

    [Fact]
    public async Task MovePageAsync_NegativeToIndex_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => PdfOperations.MovePageAsync(CreateNPagePdf(2), 0, -1));
    }

    [Fact]
    public async Task ReorderPagesAsync_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => PdfOperations.ReorderPagesAsync(CreateNPagePdf(2), [0, -1]));
    }

    // ── Helper ───────────────────────────────────────────────────────────

    private static byte[] CreateNPagePdf(int pageCount)
    {
        using var doc = new PdfDocument();
        for (int i = 0; i < pageCount; i++)
        {
            using var page = PdfPage.A4();
            page.SetFont(StandardFont.Helvetica, 12)
                .TextAt(72, 700, $"Page {i + 1}");
            doc.AddPage(page);
        }
        return doc.SaveToBytes();
    }
}
