namespace OxidizePdf.NET.Tests;

public class PdfDocumentOutlineTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetOutline_SingleItem_ReturnsSameInstance()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);

        var outline = new PdfOutline();
        outline.AddItem(new PdfOutlineItem("Chapter 1", pageIndex: 0));

        var result = doc.SetOutline(outline);

        Assert.Same(doc, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetOutline_NestedItems_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        for (var i = 0; i < 3; i++)
        {
            using var page = PdfPage.A4();
            doc.AddPage(page);
        }

        var outline = new PdfOutline();
        var chapter = new PdfOutlineItem("Chapter 1", pageIndex: 0)
        {
            IsBold = true,
            Children =
            [
                new PdfOutlineItem("Section 1.1", pageIndex: 1),
                new PdfOutlineItem("Section 1.2", pageIndex: 2),
            ],
        };
        outline.AddItem(chapter);

        doc.SetOutline(outline);
        var bytes = doc.SaveToBytes();

        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetOutline_WithFormattingOptions_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);

        var outline = new PdfOutline();
        outline.AddItem(new PdfOutlineItem("Bold Chapter", pageIndex: 0) { IsBold = true });
        outline.AddItem(new PdfOutlineItem("Italic Chapter", pageIndex: 0) { IsItalic = true });
        outline.AddItem(new PdfOutlineItem("Closed Chapter", pageIndex: 0) { IsOpen = false });

        doc.SetOutline(outline);
        var bytes = doc.SaveToBytes();

        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetOutline_EmptyOutline_ProducesValidPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        doc.AddPage(page);

        var outline = new PdfOutline();

        doc.SetOutline(outline);
        var bytes = doc.SaveToBytes();

        Assert.True(bytes.Length > 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetOutline_NullOutline_ThrowsArgumentNullException()
    {
        using var doc = new PdfDocument();

        Assert.Throws<ArgumentNullException>(() => doc.SetOutline(null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddItem_NullItem_ThrowsArgumentNullException()
    {
        var outline = new PdfOutline();

        Assert.Throws<ArgumentNullException>(() => outline.AddItem(null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PdfOutlineItem_NullTitle_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfOutlineItem(null!, pageIndex: 0));
    }
}
