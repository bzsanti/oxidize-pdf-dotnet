namespace OxidizePdf.NET.Tests;

/// <summary>
/// AddPage takes a snapshot of the page (the native layer clones it), so any
/// edit made after AddPage would be silently lost. The page is therefore
/// "consumed" on add: further mutation must fail loudly rather than no-op
/// (issue #58).
/// </summary>
public class PdfPageConsumeTests
{
    [Fact]
    public void AddPage_ThenModifyPage_ThrowsInvalidOperationException()
    {
        using var doc = new PdfDocument();
        var page = PdfPage.A4();

        doc.AddPage(page);

        Assert.Throws<InvalidOperationException>(() => page.SetRotation(90));
    }

    [Fact]
    public void AddPage_SamePageTwice_ThrowsInvalidOperationException()
    {
        using var doc = new PdfDocument();
        var page = PdfPage.A4();

        doc.AddPage(page);

        Assert.Throws<InvalidOperationException>(() => doc.AddPage(page));
    }

    [Fact]
    public void Dispose_AfterAddPage_DoesNotThrow()
    {
        using var doc = new PdfDocument();
        var page = PdfPage.A4();
        doc.AddPage(page);

        // Disposing a consumed page is still valid (frees its own clone source).
        page.Dispose();
    }
}
