namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfTable class — table creation, rows, and page rendering.
/// </summary>
public class PdfTableTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void CreateTable_WithHeaders_IsNotNull()
    {
        using var table = new PdfTable(["Name", "Age", "City"], totalWidth: 400);
        Assert.NotNull(table);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddRow_ThenAddToPage_ProducesValidPdf()
    {
        var table = new PdfTable(["Name", "Age", "City"], totalWidth: 400);
        table.SetPosition(50, 700)
            .AddRow(["Alice", "30", "Madrid"])
            .AddRow(["Bob", "25", "London"]);

        using var page = PdfPage.A4();
        using var doc = new PdfDocument();
        page.AddTable(table);
        doc.AddPage(page);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public void CreateTable_NullHeaders_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfTable(null!, 400));
    }

    [Fact]
    public void CreateTable_EmptyHeaders_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PdfTable(Array.Empty<string>(), 400));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddTable_ThenModify_ThrowsInvalidOperationException()
    {
        var table = new PdfTable(["Col1"], totalWidth: 200);
        table.AddRow(["Data"]);

        using var page = PdfPage.A4();
        page.AddTable(table);

        Assert.Throws<InvalidOperationException>(() => table.AddRow(["More"]));
    }
}
