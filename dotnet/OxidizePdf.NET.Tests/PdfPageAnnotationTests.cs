namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for annotation creation on PdfPage (ANN-001 to ANN-005).
/// </summary>
public class PdfPageAnnotationTests
{
    // ── ANN-001: Link annotations ────────────────────────────────────────────

    [Fact]
    public void AddLinkUri_ValidArgs_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddLinkUri(100, 700, 200, 20, "https://example.com");
        Assert.Same(page, result);
    }

    [Fact]
    public void AddLinkUri_NullUri_ThrowsArgumentNullException()
    {
        using var page = new PdfPage(595, 842);
        Assert.Throws<ArgumentNullException>(() => page.AddLinkUri(100, 700, 200, 20, null!));
    }

    [Fact]
    public void AddLinkGoToPage_ValidArgs_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddLinkGoToPage(100, 700, 200, 20, 2);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddLinkGoToPage_ZeroPage_ThrowsArgumentOutOfRangeException()
    {
        using var page = new PdfPage(595, 842);
        Assert.Throws<ArgumentOutOfRangeException>(() => page.AddLinkGoToPage(100, 700, 200, 20, 0));
    }

    [Fact]
    public void AddLinkGoToPage_NegativePage_ThrowsArgumentOutOfRangeException()
    {
        using var page = new PdfPage(595, 842);
        Assert.Throws<ArgumentOutOfRangeException>(() => page.AddLinkGoToPage(100, 700, 200, 20, -1));
    }

    // ── ANN-002: Markup annotations ──────────────────────────────────────────

    [Fact]
    public void AddHighlight_DefaultColor_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddHighlight(100, 700, 200, 15);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddHighlight_CustomColor_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddHighlight(100, 700, 200, 15, r: 0.0, g: 1.0, b: 0.0);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddUnderline_ValidArgs_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddUnderline(100, 700, 200, 15);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddStrikeOut_ValidArgs_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddStrikeOut(100, 700, 200, 15);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddMarkup_FluentChaining_AllThreeTypes()
    {
        using var page = new PdfPage(595, 842);
        var result = page
            .AddHighlight(100, 700, 200, 15)
            .AddUnderline(100, 680, 200, 15)
            .AddStrikeOut(100, 660, 200, 15);
        Assert.Same(page, result);
    }

    // ── ANN-003: Text notes ──────────────────────────────────────────────────

    [Fact]
    public void AddTextNote_WithContents_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddTextNote(100, 700, "This is a note");
        Assert.Same(page, result);
    }

    [Fact]
    public void AddTextNote_NullContents_ThrowsArgumentNullException()
    {
        using var page = new PdfPage(595, 842);
        Assert.Throws<ArgumentNullException>(() => page.AddTextNote(100, 700, null!));
    }

    [Fact]
    public void AddTextNote_OpenNote_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddTextNote(100, 700, "Open note", open: true);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddTextNote_AllIcons_ReturnPage()
    {
        using var page = new PdfPage(595, 842);
        foreach (TextNoteIcon icon in Enum.GetValues<TextNoteIcon>())
        {
            page.AddTextNote(100, 700, $"Icon {icon}", icon);
        }
    }

    // ── ANN-004: Stamps ──────────────────────────────────────────────────────

    [Fact]
    public void AddStamp_Draft_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddStamp(100, 700, 200, 50, StampType.Draft);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddStamp_AllStandard_ReturnPage()
    {
        using var page = new PdfPage(595, 842);
        foreach (StampType stamp in Enum.GetValues<StampType>())
        {
            page.AddStamp(100, 700, 200, 50, stamp);
        }
    }

    [Fact]
    public void AddCustomStamp_ValidName_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddCustomStamp(100, 700, 200, 50, "MyCustomStamp");
        Assert.Same(page, result);
    }

    [Fact]
    public void AddCustomStamp_NullName_ThrowsArgumentNullException()
    {
        using var page = new PdfPage(595, 842);
        Assert.Throws<ArgumentNullException>(() => page.AddCustomStamp(100, 700, 200, 50, null!));
    }

    // ── ANN-005: Geometric annotations ───────────────────────────────────────

    [Fact]
    public void AddAnnotationLine_ValidArgs_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddAnnotationLine(100, 700, 300, 700);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddAnnotationLine_WithColor_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddAnnotationLine(100, 700, 300, 700, r: 1.0, g: 0.0, b: 0.0);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddAnnotationRect_NoFill_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddAnnotationRect(100, 700, 200, 100);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddAnnotationRect_WithFill_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddAnnotationRect(100, 700, 200, 100,
            strokeR: 0.0, strokeG: 0.0, strokeB: 0.0,
            fillR: 0.9, fillG: 0.9, fillB: 0.9);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddAnnotationCircle_NoFill_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddAnnotationCircle(100, 700, 100, 100);
        Assert.Same(page, result);
    }

    [Fact]
    public void AddAnnotationCircle_WithFill_ReturnsPage()
    {
        using var page = new PdfPage(595, 842);
        var result = page.AddAnnotationCircle(100, 700, 100, 100,
            strokeR: 0.0, strokeG: 0.0, strokeB: 1.0,
            fillR: 0.8, fillG: 0.8, fillB: 1.0);
        Assert.Same(page, result);
    }

    // ── Integration: annotations survive serialization ───────────────────────

    [Fact]
    public async Task AnnotationsOnPage_DocumentSavedToBytes_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = new PdfPage(595, 842);

        page.AddLinkUri(100, 700, 200, 20, "https://example.com")
            .AddHighlight(100, 680, 200, 15)
            .AddTextNote(50, 600, "A note")
            .AddStamp(200, 500, 150, 40, StampType.Draft)
            .AddAnnotationLine(50, 400, 500, 400, r: 1.0)
            .AddAnnotationRect(50, 300, 200, 80, fillR: 0.9, fillG: 0.9, fillB: 0.9)
            .AddAnnotationCircle(300, 300, 80, 80, strokeR: 0.0, strokeG: 0.0, strokeB: 1.0);

        doc.AddPage(page);
        var bytes = doc.SaveToBytes();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100, "PDF should have substantial content");
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);

        // Round-trip: verify the generated PDF is parseable
        var extractor = new PdfExtractor();
        var pageCount = await extractor.GetPageCountAsync(bytes);
        Assert.Equal(1, pageCount);
    }

    [Fact]
    public void MultipleAnnotations_FluentChain_AllAdded()
    {
        using var page = new PdfPage(595, 842);
        page.AddHighlight(10, 10, 100, 10)
            .AddUnderline(10, 30, 100, 10)
            .AddStrikeOut(10, 50, 100, 10)
            .AddTextNote(10, 70, "Note 1")
            .AddTextNote(10, 90, "Note 2")
            .AddAnnotationLine(10, 110, 200, 110)
            .AddAnnotationRect(10, 130, 100, 50)
            .AddAnnotationCircle(10, 200, 50, 50);
        // No exception = success
    }

    [Fact]
    public void Annotations_OnDisposedPage_ThrowsObjectDisposedException()
    {
        var page = new PdfPage(595, 842);
        page.Dispose();

        Assert.Throws<ObjectDisposedException>(() => page.AddHighlight(10, 10, 100, 10));
        Assert.Throws<ObjectDisposedException>(() => page.AddTextNote(10, 10, "test"));
        Assert.Throws<ObjectDisposedException>(() => page.AddAnnotationLine(10, 10, 100, 100));
    }
}
