namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for ordered and unordered list operations on pages.
/// </summary>
public class PdfListTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void AddOrderedList_ProducesValidPdf()
    {
        using var page = PdfPage.A4();
        using var doc = new PdfDocument();
        page.SetFont(StandardFont.Helvetica, 12)
            .AddOrderedList(["First item", "Second item", "Third item"], 50, 700);
        doc.AddPage(page);
        Assert.True(doc.SaveToBytes().Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddUnorderedList_ProducesValidPdf()
    {
        using var page = PdfPage.A4();
        using var doc = new PdfDocument();
        page.SetFont(StandardFont.Helvetica, 12)
            .AddUnorderedList(["Item A", "Item B", "Item C"], 50, 600, BulletStyle.Disc);
        doc.AddPage(page);
        Assert.True(doc.SaveToBytes().Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddOrderedList_UpperRoman_ProducesValidPdf()
    {
        using var page = PdfPage.A4();
        using var doc = new PdfDocument();
        page.SetFont(StandardFont.Helvetica, 12)
            .AddOrderedList(["Step one", "Step two"], 50, 700, OrderedListStyle.UpperRoman);
        doc.AddPage(page);
        Assert.True(doc.SaveToBytes().Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddOrderedList_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12);
        Assert.Same(page, page.AddOrderedList(["Item"], 50, 700));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddUnorderedList_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.Helvetica, 12);
        Assert.Same(page, page.AddUnorderedList(["Item"], 50, 700));
    }

    [Fact]
    public void AddOrderedList_NullItems_ThrowsArgumentNullException()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.AddOrderedList(null!, 50, 700));
    }

    [Fact]
    public void AddUnorderedList_NullItems_ThrowsArgumentNullException()
    {
        using var page = PdfPage.A4();
        Assert.Throws<ArgumentNullException>(() => page.AddUnorderedList(null!, 50, 700));
    }
}
