namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfTextSpan and PdfRichText model classes.
/// </summary>
public class PdfRichTextTests
{
    // ── PdfTextSpan ──────────────────────────────────────────────────────

    [Fact]
    public void TextSpan_Constructor_SetsProperties()
    {
        var span = new PdfTextSpan("Hello", StandardFont.HelveticaBold, 14.0, 1.0, 0.0, 0.0);
        Assert.Equal("Hello", span.Text);
        Assert.Equal(StandardFont.HelveticaBold, span.Font);
        Assert.Equal(14.0, span.FontSize);
        Assert.Equal(1.0, span.R);
        Assert.Equal(0.0, span.G);
        Assert.Equal(0.0, span.B);
    }

    [Fact]
    public void TextSpan_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PdfTextSpan(null!, StandardFont.Helvetica, 12, 0, 0, 0));
    }

    // ── PdfRichText ──────────────────────────────────────────────────────

    [Fact]
    public void RichText_Constructor_StoresSpans()
    {
        var span1 = new PdfTextSpan("Hello", StandardFont.Helvetica, 12, 0, 0, 0);
        var span2 = new PdfTextSpan("World", StandardFont.HelveticaBold, 12, 1, 0, 0);
        var rich = new PdfRichText(span1, span2);

        Assert.Equal(2, rich.Spans.Count);
        Assert.Equal("Hello", rich.Spans[0].Text);
        Assert.Equal("World", rich.Spans[1].Text);
    }

    [Fact]
    public void RichText_EmptySpans_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PdfRichText());
    }

    [Fact]
    public void RichText_NullSpans_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfRichText(null!));
    }

    // ── PdfSimpleTable ───────────────────────────────────────────────────

    [Fact]
    public void SimpleTable_Constructor_SetsProperties()
    {
        var table = new PdfSimpleTable([100.0, 200.0], ["A", "B"]);
        Assert.Equal([100.0, 200.0], table.ColumnWidths);
        Assert.NotNull(table.Headers);
        Assert.Equal(["A", "B"], table.Headers!);
    }

    [Fact]
    public void SimpleTable_AddRow_FluentChaining()
    {
        var table = new PdfSimpleTable([100.0, 200.0]);
        var result = table.AddRow("a", "b");
        Assert.Same(table, result);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void SimpleTable_NullColumnWidths_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfSimpleTable(null!));
    }

    [Fact]
    public void SimpleTable_EmptyColumnWidths_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PdfSimpleTable(Array.Empty<double>()));
    }

    [Fact]
    public void SimpleTable_NoHeaders_HeadersIsNull()
    {
        var table = new PdfSimpleTable([100.0, 200.0]);
        Assert.Null(table.Headers);
    }
}
