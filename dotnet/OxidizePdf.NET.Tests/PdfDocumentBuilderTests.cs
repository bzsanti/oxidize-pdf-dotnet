namespace OxidizePdf.NET.Tests;

/// <summary>
/// Tests for PdfDocumentBuilder — high-level document builder with automatic layout.
/// </summary>
public class PdfDocumentBuilderTests
{
    // ── Creation ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void CreateA4_IsNotNull()
    {
        using var builder = PdfDocumentBuilder.A4();
        Assert.NotNull(builder);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CreateCustom_IsNotNull()
    {
        using var builder = PdfDocumentBuilder.Create(595, 842, 50, 50, 50, 50);
        Assert.NotNull(builder);
    }

    // ── Fluent chaining ──────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void AddText_ReturnsThisForChaining()
    {
        using var builder = PdfDocumentBuilder.A4();
        var result = builder.AddText("Hello", StandardFont.Helvetica, 12);
        Assert.Same(builder, result);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddSpacer_ReturnsThisForChaining()
    {
        using var builder = PdfDocumentBuilder.A4();
        var result = builder.AddSpacer(20);
        Assert.Same(builder, result);
    }

    // ── Build ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Build_WithText_ProducesDocumentWithOnePage()
    {
        using var builder = PdfDocumentBuilder.A4();
        builder.AddText("Hello World", StandardFont.Helvetica, 12);
        using var doc = builder.Build();

        Assert.Equal(1, doc.PageCount);
        var bytes = doc.SaveToBytes();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Build_WithMultipleElements_ProducesValidPdf()
    {
        using var builder = PdfDocumentBuilder.A4();
        builder.AddText("Invoice #001", StandardFont.HelveticaBold, 18)
               .AddSpacer(10)
               .AddText("Date: 2026-04-13", StandardFont.Helvetica, 12)
               .AddSpacer(20)
               .AddTextWithLineHeight("Body text", StandardFont.Helvetica, 10, 1.5);

        using var doc = builder.Build();
        Assert.True(doc.PageCount >= 1);
        Assert.True(doc.SaveToBytes().Length > 100);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Build_WithTable_ProducesValidPdf()
    {
        var table = new PdfSimpleTable([200.0, 200.0], ["Name", "Age"])
            .AddRow("Alice", "30")
            .AddRow("Bob", "25");

        using var builder = PdfDocumentBuilder.A4();
        builder.AddText("Table:", StandardFont.HelveticaBold, 14)
               .AddSpacer(10)
               .AddTable(table);

        using var doc = builder.Build();
        Assert.True(doc.PageCount >= 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Build_WithRichText_ProducesValidPdf()
    {
        var rich = new PdfRichText(
            new PdfTextSpan("Total: ", StandardFont.HelveticaBold, 14, 0, 0, 0),
            new PdfTextSpan("$1,234.56", StandardFont.Helvetica, 14, 0.3, 0.3, 0.3));

        using var builder = PdfDocumentBuilder.A4();
        builder.AddRichText(rich);

        using var doc = builder.Build();
        Assert.Equal(1, doc.PageCount);
    }

    // ── Error handling ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Build_CalledTwice_ThrowsInvalidOperationException()
    {
        using var builder = PdfDocumentBuilder.A4();
        builder.AddText("Hello", StandardFont.Helvetica, 12);
        using var doc = builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddText_AfterBuild_ThrowsInvalidOperationException()
    {
        using var builder = PdfDocumentBuilder.A4();
        builder.AddText("Hello", StandardFont.Helvetica, 12);
        using var doc = builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddText("More", StandardFont.Helvetica, 12));
    }

    [Fact]
    public void AddText_NullText_ThrowsArgumentNullException()
    {
        using var builder = PdfDocumentBuilder.A4();
        Assert.Throws<ArgumentNullException>(() => builder.AddText(null!, StandardFont.Helvetica, 12));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Dispose_ThenBuild_ThrowsObjectDisposedException()
    {
        var builder = PdfDocumentBuilder.A4();
        builder.Dispose();
        Assert.Throws<ObjectDisposedException>(() => builder.Build());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Dispose_Twice_DoesNotThrow()
    {
        var builder = PdfDocumentBuilder.A4();
        builder.Dispose();
        builder.Dispose(); // Should not throw
    }

    [Fact]
    public void AddTable_NullTable_ThrowsArgumentNullException()
    {
        using var builder = PdfDocumentBuilder.A4();
        Assert.Throws<ArgumentNullException>(() => builder.AddTable(null!));
    }

    [Fact]
    public void AddRichText_NullRichText_ThrowsArgumentNullException()
    {
        using var builder = PdfDocumentBuilder.A4();
        Assert.Throws<ArgumentNullException>(() => builder.AddRichText(null!));
    }
}
