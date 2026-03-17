namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for header and footer page operations.
/// </summary>
public class PdfHeaderFooterTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetHeader_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetHeader("Document Title", StandardFont.HelveticaBold, 14.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFooter_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        Assert.Same(page, page.SetFooter("Page 1", StandardFont.Helvetica, 10.0));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetHeader_AndSaveDocument_ProducesValidPdf()
    {
        using var page = PdfPage.A4();
        using var doc = new PdfDocument();
        page.SetHeader("Report Header", StandardFont.HelveticaBold, 14.0)
            .SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 700, "Body text");
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFooter_AndSaveDocument_ProducesValidPdf()
    {
        using var page = PdfPage.A4();
        using var doc = new PdfDocument();
        page.SetFooter("Confidential", StandardFont.Helvetica, 8.0)
            .SetFont(StandardFont.Helvetica, 12)
            .TextAt(50, 700, "Body text");
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void SetHeader_NullContent_ThrowsArgumentNullException()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.SetHeader(null!, StandardFont.Helvetica, 12));
    }

    [Fact]
    public void SetFooter_NullContent_ThrowsArgumentNullException()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.SetFooter(null!, StandardFont.Helvetica, 12));
    }
}
