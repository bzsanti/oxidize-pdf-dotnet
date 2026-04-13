namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfFlowLayout — automatic flow layout engine with page break support.
/// </summary>
public class PdfFlowLayoutTests
{
    // ── Creation ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void CreateA4_IsNotNull()
    {
        using var layout = PdfFlowLayout.A4();
        Assert.NotNull(layout);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CreateCustom_IsNotNull()
    {
        using var layout = PdfFlowLayout.Create(595, 842, 50, 50, 50, 50);
        Assert.NotNull(layout);
    }

    // ── Properties ───────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void ContentWidth_A4_Returns451()
    {
        using var layout = PdfFlowLayout.A4();
        Assert.Equal(451.0, layout.ContentWidth, precision: 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void UsableHeight_A4_Returns698()
    {
        using var layout = PdfFlowLayout.A4();
        Assert.Equal(698.0, layout.UsableHeight, precision: 1);
    }

    // ── Fluent chaining ──────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void AddText_ReturnsThisForChaining()
    {
        using var layout = PdfFlowLayout.A4();
        var result = layout.AddText("Hello", StandardFont.Helvetica, 12);
        Assert.Same(layout, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddSpacer_ReturnsThisForChaining()
    {
        using var layout = PdfFlowLayout.A4();
        var result = layout.AddSpacer(20);
        Assert.Same(layout, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddTextWithLineHeight_ReturnsThisForChaining()
    {
        using var layout = PdfFlowLayout.A4();
        var result = layout.AddTextWithLineHeight("Hello", StandardFont.Helvetica, 12, 1.5);
        Assert.Same(layout, result);
    }

    // ── BuildInto ────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void BuildInto_WithText_ProducesDocumentWithOnePage()
    {
        using var layout = PdfFlowLayout.A4();
        layout.AddText("Hello World", StandardFont.Helvetica, 12);

        using var doc = new PdfDocument();
        layout.BuildInto(doc);

        Assert.Equal(1, doc.PageCount);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void BuildInto_WithMultipleElements_ProducesValidPdf()
    {
        using var layout = PdfFlowLayout.A4();
        layout.AddText("Title", StandardFont.HelveticaBold, 18)
              .AddSpacer(10)
              .AddText("Body text", StandardFont.Helvetica, 12)
              .AddSpacer(20)
              .AddTextWithLineHeight("More text", StandardFont.Helvetica, 10, 1.5);

        using var doc = new PdfDocument();
        layout.BuildInto(doc);

        Assert.True(doc.PageCount >= 1);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void BuildInto_WithTable_ProducesValidPdf()
    {
        using var layout = PdfFlowLayout.A4();
        var table = new PdfSimpleTable([200.0, 200.0], ["Name", "Age"])
            .AddRow("Alice", "30")
            .AddRow("Bob", "25");

        layout.AddText("Table:", StandardFont.HelveticaBold, 14)
              .AddSpacer(10)
              .AddTable(table);

        using var doc = new PdfDocument();
        layout.BuildInto(doc);

        Assert.True(doc.PageCount >= 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void BuildInto_WithRichText_ProducesValidPdf()
    {
        using var layout = PdfFlowLayout.A4();
        var rich = new PdfRichText(
            new PdfTextSpan("Total: ", StandardFont.HelveticaBold, 14, 0, 0, 0),
            new PdfTextSpan("$1,234.56", StandardFont.Helvetica, 14, 0.3, 0.3, 0.3));

        layout.AddRichText(rich);

        using var doc = new PdfDocument();
        layout.BuildInto(doc);

        Assert.Equal(1, doc.PageCount);
    }

    // ── Error handling ───────────────────────────────────────────────────

    [Fact]
    public void AddText_NullText_ThrowsArgumentNullException()
    {
        using var layout = PdfFlowLayout.A4();
        Assert.Throws<ArgumentNullException>(() => layout.AddText(null!, StandardFont.Helvetica, 12));
    }

    [Fact]
    public void BuildInto_NullDocument_ThrowsArgumentNullException()
    {
        using var layout = PdfFlowLayout.A4();
        Assert.Throws<ArgumentNullException>(() => layout.BuildInto(null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Dispose_ThenAddText_ThrowsObjectDisposedException()
    {
        var layout = PdfFlowLayout.A4();
        layout.Dispose();
        Assert.Throws<ObjectDisposedException>(() => layout.AddText("Test", StandardFont.Helvetica, 12));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Dispose_Twice_DoesNotThrow()
    {
        var layout = PdfFlowLayout.A4();
        layout.Dispose();
        layout.Dispose(); // Should not throw
    }

    [Fact]
    public void AddTable_NullTable_ThrowsArgumentNullException()
    {
        using var layout = PdfFlowLayout.A4();
        Assert.Throws<ArgumentNullException>(() => layout.AddTable(null!));
    }

    [Fact]
    public void AddRichText_NullRichText_ThrowsArgumentNullException()
    {
        using var layout = PdfFlowLayout.A4();
        Assert.Throws<ArgumentNullException>(() => layout.AddRichText(null!));
    }
}
